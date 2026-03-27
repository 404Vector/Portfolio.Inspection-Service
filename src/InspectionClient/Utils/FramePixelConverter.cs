using System;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

using ProtoPixelFormat = Core.Grpc.FrameGrabber.PixelFormat;

namespace InspectionClient.Utils;

internal static class FramePixelConverter
{
  internal static (WriteableBitmap write, WriteableBitmap read) AllocateBuffers(int width, int height)
  {
    var pixelSize = new PixelSize(width, height);
    var dpi       = new Vector(96, 96);
    return (
        new WriteableBitmap(pixelSize, dpi, PixelFormat.Bgra8888, AlphaFormat.Opaque),
        new WriteableBitmap(pixelSize, dpi, PixelFormat.Bgra8888, AlphaFormat.Opaque));
  }

  internal static unsafe void CopyFrameToBuffer(
      byte[] pixels,
      ProtoPixelFormat fmt,
      WriteableBitmap target)
  {
    using var fb = target.Lock();

    var dst       = (byte*)fb.Address;
    var dstStride = fb.RowBytes;
    var width     = fb.Size.Width;
    var height    = fb.Size.Height;

    switch (fmt)
    {
      case ProtoPixelFormat.Mono8:
      {
        var srcStride = width;
        for (var y = 0; y < height; y++)
        {
          var srcRow = pixels.AsSpan(y * srcStride, srcStride);
          var dstRow = dst + y * dstStride;
          for (var x = 0; x < width; x++)
          {
            var v  = srcRow[x];
            var p  = dstRow + x * 4;
            p[0]   = v;
            p[1]   = v;
            p[2]   = v;
            p[3]   = 255;
          }
        }
        break;
      }

      case ProtoPixelFormat.Rgb8:
      {
        var srcStride = width * 3;
        for (var y = 0; y < height; y++)
        {
          var srcRow = pixels.AsSpan(y * srcStride, srcStride);
          var dstRow = dst + y * dstStride;
          for (var x = 0; x < width; x++)
          {
            var p  = dstRow + x * 4;
            p[0]   = srcRow[x * 3 + 2];
            p[1]   = srcRow[x * 3 + 1];
            p[2]   = srcRow[x * 3 + 0];
            p[3]   = 255;
          }
        }
        break;
      }

      case ProtoPixelFormat.Bgr8:
      default:
      {
        var srcStride = width * 3;
        for (var y = 0; y < height; y++)
        {
          var srcRow = pixels.AsSpan(y * srcStride, srcStride);
          var dstRow = dst + y * dstStride;
          for (var x = 0; x < width; x++)
          {
            var p  = dstRow + x * 4;
            p[0]   = srcRow[x * 3 + 0];
            p[1]   = srcRow[x * 3 + 1];
            p[2]   = srcRow[x * 3 + 2];
            p[3]   = 255;
          }
        }
        break;
      }
    }
  }
}
