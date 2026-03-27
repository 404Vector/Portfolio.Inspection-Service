using System.Collections.Generic;

namespace InspectionClient.Models;

/// <summary>
/// GetCapabilities RPC 응답을 UI 레이어 모델로 매핑한 결과.
/// </summary>
public record GrabberCapabilities(
    IReadOnlyList<GrabberParameterItem> Parameters,
    IReadOnlyList<GrabberCommandItem>   Commands)
{
  public static readonly GrabberCapabilities Empty =
      new([], []);
}
