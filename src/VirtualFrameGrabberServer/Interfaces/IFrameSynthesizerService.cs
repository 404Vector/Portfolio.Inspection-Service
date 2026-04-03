using Core.FrameGrabber.Models;

namespace VirtualFrameGrabberServer.Interfaces;

/// <summary>
/// GrabberConfig와 프레임 인덱스를 받아 GrabbedFrame을 합성하는 계약.
/// 구현체(Gradient, FileImage 등)를 VirtualFrameGrabberServer에서 런타임에 교체할 수 있다.
/// </summary>
public interface IFrameSynthesizerService
{
  GrabbedFrame BuildFrame(GrabberConfig config, long frameIndex);
}
