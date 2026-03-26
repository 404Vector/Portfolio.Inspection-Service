using Core.FrameGrabber.Models;
using CE = Core.Enums;

namespace FileFrameGrabberService.Services;

/// <summary>
/// SetImageSource()로 전달받은 이미지 버퍼를 소스로 GrabbedFrame을 반환한다.
/// 이미지 파일 읽기는 FileFrameGrabberService가 담당하며,
/// 이 클래스는 전달받은 픽셀 데이터만 보관하고 프레임을 조립한다.
/// </summary>
public sealed class FileImageFrameSynthesizerService : IFrameSynthesizerService
{
  private readonly Lock _lock = new();

  private byte[]?          _pixelData;
  private int              _width;
  private int              _height;
  private CE.PixelFormat   _pixelFormat;
  private int              _stride;

  /// <summary>
  /// 이미지 소스를 교체한다. 이미지 파일 읽기는 호출자가 담당한다.
  /// </summary>
  public void SetImageSource(
      byte[]         pixelData,
      int            width,
      int            height,
      CE.PixelFormat pixelFormat,
      int            stride)
  {
    lock (_lock)
    {
      _pixelData   = pixelData;
      _width       = width;
      _height      = height;
      _pixelFormat = pixelFormat;
      _stride      = stride;
    }
  }

  /// <summary>
  /// 소스가 설정되지 않은 경우 InvalidOperationException을 던진다.
  /// </summary>
  public GrabbedFrame BuildFrame(GrabberConfig config, long frameIndex)
  {
    byte[]         pixelData;
    int            width, height, stride;
    CE.PixelFormat pixelFormat;

    lock (_lock)
    {
      if (_pixelData is null)
        throw new InvalidOperationException(
            "Image source is not set. Call SetParameter(\"image_path\", ...) first.");

      pixelData   = _pixelData;
      width       = _width;
      height      = _height;
      pixelFormat = _pixelFormat;
      stride      = _stride;
    }

    return new GrabbedFrame(
        FrameId:     $"frame_{frameIndex:D8}",
        PixelData:   pixelData,
        Width:       width,
        Height:      height,
        PixelFormat: pixelFormat,
        Stride:      stride,
        Timestamp:   DateTimeOffset.UtcNow);
  }
}
