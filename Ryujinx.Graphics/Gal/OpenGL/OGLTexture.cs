using OpenTK.Graphics.OpenGL;
using Ryujinx.Common.Logging;
using Ryujinx.Graphics.Texture;
using System;
using System.Diagnostics;

namespace Ryujinx.Graphics.Gal.OpenGL
{
    class OGLTexture : IGalTexture
    {
        private const long MaxTextureCacheSize = 768 * 1024 * 1024;

        private OGLCachedResource<ImageHandler> TextureCache;

        public EventHandler<int> TextureDeleted { get; set; }

        public OGLTexture()
        {
            TextureCache = new OGLCachedResource<ImageHandler>(DeleteTexture, MaxTextureCacheSize);
        }

        public void LockCache()
        {
            TextureCache.Lock();
        }

        public void UnlockCache()
        {
            TextureCache.Unlock();
        }

        private void DeleteTexture(ImageHandler CachedImage)
        {
            TextureDeleted?.Invoke(this, CachedImage.Handle);

            GL.DeleteTexture(CachedImage.Handle);
        }

        public void Create(long Key, int Size, GalImage Image)
        {
            int Handle = GL.GenTexture();

            TextureTarget Target = ImageUtils.GetTextureTarget(Image.TextureType);

            GL.BindTexture(Target, Handle);

            const int Level  = 0; //TODO: Support mipmap textures.
            const int Border = 0;

            Debug.Assert(Image.MaxMipmapLevel != 1, "No Mipmap support");

            TextureCache.AddOrUpdate(Key, new ImageHandler(Handle, Image), (uint)Size);

            if (ImageUtils.IsCompressed(Image.Format))
            {
                throw new InvalidOperationException("Surfaces with compressed formats are not supported!");
            }

            (PixelInternalFormat InternalFmt,
             PixelFormat         Format,
             PixelType           Type) = OGLEnumConverter.GetImageFormat(Image.Format);

            switch (Target)
            {
                case TextureTarget.Texture2D:
                    GL.TexImage2D(
                        Target,
                        Level,
                        InternalFmt,
                        Image.Width,
                        Image.Height,
                        Border,
                        Format,
                        Type,
                        IntPtr.Zero);
                    break;
                case TextureTarget.Texture2DArray:
                case TextureTarget.Texture3D:
                    GL.TexImage3D(
                        Target,
                        Level,
                        InternalFmt,
                        Image.Width,
                        Image.Height,
                        Image.Depth,
                        Border,
                        Format,
                        Type,
                        IntPtr.Zero);
                    break;
                default:
                    Logger.PrintWarning(LogClass.Gpu, $"Unsupported texture target type: {Target}");
                    throw new InvalidOperationException();
                    GL.TexImage2D(
                        TextureTarget.Texture2D,
                        Level,
                        InternalFmt,
                        Image.Width,
                        Image.Height,
                        Border,
                        Format,
                        Type,
                        IntPtr.Zero);
                    break;
            }


        }

        public void Create(long Key, byte[] Data, GalImage Image)
        {
            int Handle = GL.GenTexture();

            TextureTarget Target = ImageUtils.GetTextureTarget(Image.TextureType);

            GL.BindTexture(Target, Handle);

            const int Level  = 0; //TODO: Support mipmap textures.
            const int Border = 0;

            Debug.Assert(Image.MaxMipmapLevel != 1, "No Mipmap support");

            TextureCache.AddOrUpdate(Key, new ImageHandler(Handle, Image), (uint)Data.Length);

            if (ImageUtils.IsCompressed(Image.Format) && !IsAstc(Image.Format))
            {
                InternalFormat InternalFmt = OGLEnumConverter.GetCompressedImageFormat(Image.Format);

                switch (Target)
                {
                    case TextureTarget.Texture2D:
                        GL.CompressedTexImage2D(
                            Target,
                            Level,
                            InternalFmt,
                            Image.Width,
                            Image.Height,
                            Border,
                            Data.Length,
                            Data);
                        break;
                    case TextureTarget.Texture2DArray:
                    case TextureTarget.Texture3D:
                        GL.CompressedTexImage3D(
                            Target,
                            Level,
                            InternalFmt,
                            Image.Width,
                            Image.Height,
                            Image.Depth,
                            Border,
                            Data.Length,
                            Data);
                        break;
                    case TextureTarget.TextureCubeMap:
                        for (int i = 0; i < 6; i++)
                        {
                            GL.CompressedTexImage2D(
                                TextureTarget.TextureCubeMapPositiveX + i,
                                Level,
                                InternalFmt,
                                Image.Width,
                                Image.Height,
                                Border,
                                Data.Length, // FIXME: NOT SURE OF THIS
                                Data);
                        }
                        break;
                    case TextureTarget.TextureCubeMapArray:
                        GL.CompressedTexImage3D(
                            Target,
                            Level,
                            InternalFmt,
                            Image.Width,
                            Image.Height,
                            Image.Depth * 6,
                            Border,
                            Data.Length,
                            Data);
                        break;
                    default:
                        Logger.PrintWarning(LogClass.Gpu, $"Unsupported texture target type: {Target} ({Image.Depth})");
                        throw new InvalidOperationException();
                        GL.CompressedTexImage2D(
                            TextureTarget.Texture2D,
                            Level,
                            InternalFmt,
                            Image.Width,
                            Image.Height,
                            Border,
                            Data.Length,
                            Data);
                        break;
                }
            }
            else
            {
                //TODO: Use KHR_texture_compression_astc_hdr when available
                if (IsAstc(Image.Format))
                {
                    int TextureBlockWidth  = ImageUtils.GetBlockWidth(Image.Format);
                    int TextureBlockHeight = ImageUtils.GetBlockHeight(Image.Format);
                    int TextureBlockDepth  = ImageUtils.GetBlockDepth(Image.Format);

                    Data = ASTCDecoder.DecodeToRGBA8888(
                        Data,
                        TextureBlockWidth,
                        TextureBlockHeight,
                        TextureBlockDepth,
                        Image.Width,
                        Image.Height,
                        Image.Depth);

                    Image.Format = GalImageFormat.RGBA8 | (Image.Format & GalImageFormat.TypeMask);
                }

                (PixelInternalFormat InternalFmt,
                 PixelFormat         Format,
                 PixelType           Type) = OGLEnumConverter.GetImageFormat(Image.Format);


                switch (Target)
                {
                    case TextureTarget.Texture2D:
                        GL.TexImage2D(
                            Target,
                            Level,
                            InternalFmt,
                            Image.Width,
                            Image.Height,
                            Border,
                            Format,
                            Type,
                            Data);
                        break;
                    case TextureTarget.Texture2DArray:
                    case TextureTarget.Texture3D:
                        GL.TexImage3D(
                            Target,
                            Level,
                            InternalFmt,
                            Image.Width,
                            Image.Height,
                            Image.Depth,
                            Border,
                            Format,
                            Type,
                            Data);
                        break;
                    default:
                        Logger.PrintWarning(LogClass.Gpu, $"Unsupported texture target type: {Target}");
                        throw new InvalidOperationException();
                        GL.TexImage2D(
                            TextureTarget.Texture2D,
                            Level,
                            InternalFmt,
                            Image.Width,
                            Image.Height,
                            Border,
                            Format,
                            Type,
                            Data);
                        break;
                }
            }
        }

        private static bool IsAstc(GalImageFormat Format)
        {
            Format &= GalImageFormat.FormatMask;

            return Format > GalImageFormat.Astc2DStart && Format < GalImageFormat.Astc2DEnd;
        }

        public bool TryGetImage(long Key, out GalImage Image)
        {
            if (TextureCache.TryGetValue(Key, out ImageHandler CachedImage))
            {
                Image = CachedImage.Image;

                return true;
            }

            Image = default(GalImage);

            return false;
        }

        public bool TryGetImageHandler(long Key, out ImageHandler CachedImage)
        {
            if (TextureCache.TryGetValue(Key, out CachedImage))
            {
                return true;
            }

            CachedImage = null;

            return false;
        }

        public void Bind(long Key, int Index, GalImage Image)
        {
            if (TextureCache.TryGetValue(Key, out ImageHandler CachedImage))
            {
                GL.ActiveTexture(TextureUnit.Texture0 + Index);

                TextureTarget Target = ImageUtils.GetTextureTarget(Image.TextureType);

                GL.BindTexture(Target, CachedImage.Handle);

                int[] SwizzleRgba = new int[]
                {
                    (int)OGLEnumConverter.GetTextureSwizzle(Image.XSource),
                    (int)OGLEnumConverter.GetTextureSwizzle(Image.YSource),
                    (int)OGLEnumConverter.GetTextureSwizzle(Image.ZSource),
                    (int)OGLEnumConverter.GetTextureSwizzle(Image.WSource)
                };

                GL.TexParameter(Target, TextureParameterName.TextureSwizzleRgba, SwizzleRgba);
            }
        }

        public void SetSampler(GalImage Image, GalTextureSampler Sampler)
        {
            int WrapS = (int)OGLEnumConverter.GetTextureWrapMode(Sampler.AddressU);
            int WrapT = (int)OGLEnumConverter.GetTextureWrapMode(Sampler.AddressV);
            int WrapR = (int)OGLEnumConverter.GetTextureWrapMode(Sampler.AddressP);

            int MinFilter = (int)OGLEnumConverter.GetTextureMinFilter(Sampler.MinFilter, Sampler.MipFilter);
            int MagFilter = (int)OGLEnumConverter.GetTextureMagFilter(Sampler.MagFilter);

            TextureTarget Target = ImageUtils.GetTextureTarget(Image.TextureType);

            GL.TexParameter(Target, TextureParameterName.TextureWrapS, WrapS);
            GL.TexParameter(Target, TextureParameterName.TextureWrapT, WrapT);
            GL.TexParameter(Target, TextureParameterName.TextureWrapR, WrapR);

            GL.TexParameter(Target, TextureParameterName.TextureMinFilter, MinFilter);
            GL.TexParameter(Target, TextureParameterName.TextureMagFilter, MagFilter);

            float[] Color = new float[]
            {
                Sampler.BorderColor.Red,
                Sampler.BorderColor.Green,
                Sampler.BorderColor.Blue,
                Sampler.BorderColor.Alpha
            };

            GL.TexParameter(Target, TextureParameterName.TextureBorderColor, Color);
        }
    }
}
