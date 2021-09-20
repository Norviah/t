﻿using System;

namespace Ryujinx.Graphics.Video
{
    public interface ISurface : IDisposable
    {
        Plane YPlane { get; }
        Plane UPlane { get; }
        Plane VPlane { get; }

        int Width { get; }
        int Height { get; }
        int Stride { get; }
        int UvWidth { get; }
        int UvHeight { get; }
        int UvStride { get; }
        int FrameNumber { get; }
    }
}
