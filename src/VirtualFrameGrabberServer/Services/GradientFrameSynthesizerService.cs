using Core.FrameGrabber.Models;
using VirtualFrameGrabberServer.Interfaces;
using CE = Core.Enums;

namespace VirtualFrameGrabberServer.Services;

/// <summary>
/// GrabberConfig와 프레임 인덱스를 받아 GrabbedFrame을 합성한다.
/// 픽셀 데이터 생성 알고리즘만 책임진다.
/// </summary>
public sealed class GradientFrameSynthesizerService : IFrameSynthesizerService
{
  public GrabbedFrame BuildFrame(GrabberConfig config, long frameIndex)
  {
    int    bpp    = BytesPerPixel(config.PixelFormat);
    int    stride = config.Width * bpp;
    byte[] data   = GenerateGradient(config, frameIndex, stride);

    return new GrabbedFrame(
        FrameId:     $"frame_{frameIndex:D8}",
        PixelData:   data,
        Width:       config.Width,
        Height:      config.Height,
        PixelFormat: config.PixelFormat,
        Stride:      stride,
        Timestamp:   DateTimeOffset.UtcNow);
  }

  private static byte[] GenerateGradient(GrabberConfig config, long frameIndex, int stride)
  {
    int    bpp    = BytesPerPixel(config.PixelFormat);
    byte[] data   = new byte[stride * config.Height];
    int    offset = (int)(frameIndex * 2 % 256);

    for (int y = 0; y < config.Height; y++)
    for (int x = 0; x < config.Width;  x++)
    {
      byte v   = (byte)((x + y + offset) % 256);
      int  idx = y * stride + x * bpp;

      if (config.PixelFormat == CE.PixelFormat.Mono8)
      {
        data[idx] = v;
      }
      else
      {
        data[idx]     = v;
        data[idx + 1] = (byte)((v + 85)  % 256);
        data[idx + 2] = (byte)((v + 170) % 256);
      }
    }

    return data;
  }

  private static int BytesPerPixel(CE.PixelFormat fmt) => fmt switch
  {
    CE.PixelFormat.Mono8                        => 1,
    CE.PixelFormat.Rgb8 or CE.PixelFormat.Bgr8 => 3,
    _                                           => 1
  };
}
