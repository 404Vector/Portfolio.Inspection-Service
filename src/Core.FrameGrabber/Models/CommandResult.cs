namespace Core.FrameGrabber.Models;

/// <summary>
/// ExecuteCommandAsync의 반환값.
/// 성공 시 ReturnValue는 null일 수 있다 (반환값 없는 명령).
/// 실패 시 ReturnValue가 StringValue이면 오류 메시지.
/// </summary>
public record CommandResult(
    bool            Success,
    ParameterValue? ReturnValue = null);
