namespace Core.Interfaces;

/// <summary>
/// 단일 프레임 검사 결과의 공통 계약입니다.
/// 프레임 수준의 합격/불합격은 결함 목록(Defects)의 유무로 판단하므로
/// Status를 별도로 노출하지 않습니다.
/// </summary>
public interface IInspectionResult {
  string         FrameId     { get; }
  DateTimeOffset InspectedAt { get; }
}
