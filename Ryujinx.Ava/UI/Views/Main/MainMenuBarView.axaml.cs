﻿using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using LibHac.Ncm;
using LibHac.Tools.FsSystem.NcaUtils;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Ava.UI.ViewModels;
using Ryujinx.Ava.UI.Windows;
using Ryujinx.Common;
using Ryujinx.Common.Utilities;
using Ryujinx.HLE.HOS;
using Ryujinx.Modules;
using Ryujinx.Ui.Common.Configuration;
using Ryujinx.Ui.Common.Helper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Ryujinx.Ava.UI.Views.Main
{
    public partial class MainMenuBarView : UserControl
    {
        public MainWindow Window { get; private set; }
        public MainWindowViewModel ViewModel { get; private set; }

        public MainMenuBarView()
        {
            InitializeComponent();

            HideFileTypesMenuItem.Items = GenerateHideFileTypeItems();
            ChangeLanguageMenuItem.Items = GenerateLanguageMenuItems();
        }

        private CheckBox[] GenerateHideFileTypeItems()
        {
            return new List<CheckBox>()
            {
                new CheckBox()
                {
                    Content = ".NSP",
                    IsChecked = ConfigurationState.Instance.Ui.ShownFileTypes.NSP.Value,
                    Command = MiniCommand.Create(() => ViewModel.HideFileType("nsp"))
                },
                new CheckBox()
                {
                    Content = ".PFS0",
                    IsChecked = ConfigurationState.Instance.Ui.ShownFileTypes.PFS0.Value,
                    Command = MiniCommand.Create(() => ViewModel.HideFileType("pfs0"))
                },
                new CheckBox()
                {
                    Content = ".XCI",
                    IsChecked = ConfigurationState.Instance.Ui.ShownFileTypes.XCI.Value,
                    Command = MiniCommand.Create(() => ViewModel.HideFileType("xci"))
                },
                new CheckBox()
                {
                    Content = ".NCA",
                    IsChecked = ConfigurationState.Instance.Ui.ShownFileTypes.NCA.Value,
                    Command = MiniCommand.Create(() => ViewModel.HideFileType("nca"))
                },
                new CheckBox()
                {
                    Content = ".NRO",
                    IsChecked = ConfigurationState.Instance.Ui.ShownFileTypes.NRO.Value,
                    Command = MiniCommand.Create(() => ViewModel.HideFileType("nro"))
                },
                new CheckBox()
                {
                    Content = ".NSO",
                    IsChecked = ConfigurationState.Instance.Ui.ShownFileTypes.NSO.Value,
                    Command = MiniCommand.Create(() => ViewModel.HideFileType("nso"))
                }
            }.ToArray();
        }

        private MenuItem[] GenerateLanguageMenuItems()
        {
            List<MenuItem> menuItems = new();

            string localePath = "Ryujinx.Ava/Assets/Locales";
            string localeExt  = ".json";

            string[] localesPath = EmbeddedResources.GetAllAvailableResources(localePath, localeExt);

            Array.Sort(localesPath);

            foreach (string locale in localesPath)
            {
                string languageCode = Path.GetFileNameWithoutExtension(locale).Split('.').Last();
                string languageJson = EmbeddedResources.ReadAllText($"{localePath}/{languageCode}{localeExt}");
                var    strings      = JsonHelper.Deserialize<Dictionary<string, string>>(languageJson);

                if (!strings.TryGetValue("Language", out string languageName))
                {
                    languageName = languageCode;
                }

                MenuItem menuItem = new()
                {
                    Header  = languageName,
                    Command = MiniCommand.Create(() =>
                    {
                        ViewModel.ChangeLanguage(languageCode);
                    })
                };

                menuItems.Add(menuItem);
            }

            return menuItems.ToArray();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            if (VisualRoot is MainWindow window)
            {
                Window = window;
            }

            ViewModel = Window.ViewModel;
            DataContext = ViewModel;
        }

        private async void StopEmulation_Click(object sender, RoutedEventArgs e)
        {
            await Window.ViewModel.AppHost?.ShowExitPrompt();
        }

        private async void PauseEmulation_Click(object sender, RoutedEventArgs e)
        {
            await Task.Run(() =>
            {
                Window.ViewModel.AppHost?.Pause();
            });
        }

        private async void ResumeEmulation_Click(object sender, RoutedEventArgs e)
        {
            await Task.Run(() =>
            {
                Window.ViewModel.AppHost?.Resume();
            });
        }

        public async void OpenSettings(object sender, RoutedEventArgs e)
        {
            Window.SettingsWindow = new(Window.VirtualFileSystem, Window.ContentManager);

            await Window.SettingsWindow.ShowDialog(Window);

            ViewModel.LoadConfigurableHotKeys();
        }

        public void OpenMiiApplet(object sender, RoutedEventArgs e)
        {
            string contentPath = ViewModel.ContentManager.GetInstalledContentPath(0x0100000000001009, StorageId.BuiltInSystem, NcaContentType.Program);

            if (!string.IsNullOrEmpty(contentPath))
            {
                ViewModel.LoadApplication(contentPath, false, "Mii Applet");
            }
        }

        public async void OpenAmiiboWindow(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.IsAmiiboRequested)
            {
                return;
            }

            if (ViewModel.AppHost.Device.System.SearchingForAmiibo(out int deviceId))
            {
                string titleId = ViewModel.AppHost.Device.Application.TitleIdText.ToUpper();
                AmiiboWindow window = new(ViewModel.ShowAll, ViewModel.LastScannedAmiiboId, titleId);

                await window.ShowDialog(Window);

                if (window.IsScanned)
                {
                    ViewModel.ShowAll = window.ViewModel.ShowAllAmiibo;
                    ViewModel.LastScannedAmiiboId = window.ScannedAmiibo.GetId();

                    ViewModel.AppHost.Device.System.ScanAmiibo(deviceId, ViewModel.LastScannedAmiiboId, window.ViewModel.UseRandomUuid);
                }
            }
        }

        public async void OpenCheatManagerForCurrentApp(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.IsGameRunning)
            {
                return;
            }

            ApplicationLoader application = ViewModel.AppHost.Device.Application;
            if (application != null)
            {
                await new CheatWindow(Window.VirtualFileSystem, application.TitleIdText, application.TitleName).ShowDialog(Window);

                ViewModel.AppHost.Device.EnableCheats();
            }
        }

        private void ScanAmiiboMenuItem_AttachedToVisualTree(object sender, VisualTreeAttachmentEventArgs e)
        {
            if (sender is MenuItem)
            {
                ViewModel.IsAmiiboRequested = Window.ViewModel.AppHost.Device.System.SearchingForAmiibo(out _);
            }
        }

        private async void InstallFileTypes_Click(object sender, RoutedEventArgs e)
        {
            if (FileAssociationHelper.Install())
            {
                await ContentDialogHelper.CreateInfoDialog(LocaleManager.Instance[LocaleKeys.DialogInstallFileTypesSuccessMessage],
                    string.Empty, LocaleManager.Instance[LocaleKeys.InputDialogOk], string.Empty, string.Empty);
            }
            else
            {
                await ContentDialogHelper.CreateErrorDialog(LocaleManager.Instance[LocaleKeys.DialogInstallFileTypesErrorMessage]);
            }
        }

        private async void UninstallFileTypes_Click(object sender, RoutedEventArgs e)
        {
            if (FileAssociationHelper.Uninstall())
            {
                await ContentDialogHelper.CreateInfoDialog(LocaleManager.Instance[LocaleKeys.DialogUninstallFileTypesSuccessMessage],
                    string.Empty, LocaleManager.Instance[LocaleKeys.InputDialogOk], string.Empty, string.Empty);
            }
            else
            {
                await ContentDialogHelper.CreateErrorDialog(LocaleManager.Instance[LocaleKeys.DialogUninstallFileTypesErrorMessage]);
            }
        }

        public async void CheckForUpdates(object sender, RoutedEventArgs e)
        {
            if (Updater.CanUpdate(true))
            {
                await Updater.BeginParse(Window, true);
            }
        }

        public async void OpenAboutWindow(object sender, RoutedEventArgs e)
        {
            await AboutWindow.Show();
        }

        public void CloseWindow(object sender, RoutedEventArgs e)
        {
            Window.Close();
        }
    }
}