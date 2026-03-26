namespace Core.FrameGrabber.Models;

/// <summary>
/// IFrameGrabber 구현체가 외부에 노출하는 명령의 메타데이터.
/// </summary>
public record CommandDescriptor(
    string  Key,
    string  DisplayName,
    string? Description = null);
