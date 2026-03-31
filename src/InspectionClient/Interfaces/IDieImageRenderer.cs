using Avalonia.Media;
using InspectionClient.Models;

namespace InspectionClient.Interfaces;

/// <summary>
/// Test Die Image를 Avalonia DrawingContext에 렌더링하는 서비스 계약.
/// 렌더링 전용. 파라미터 상태 보관은 IWorkflowStateService가 담당한다.
/// </summary>
public interface IDieImageRenderer
{
  /// <summary>
  /// 주어진 파라미터로 Die Image를 <paramref name="context"/>에 그린다.
  /// </summary>
  void Render(DrawingContext context, DieRenderingParameters parameters);
}
