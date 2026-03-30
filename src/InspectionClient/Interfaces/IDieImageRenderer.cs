using Avalonia.Media;
using InspectionClient.Models;

namespace InspectionClient.Interfaces;

/// <summary>
/// Test Die Image를 Avalonia DrawingContext에 렌더링하는 서비스 계약.
/// </summary>
public interface IDieImageRenderer
{
  /// <summary>
  /// 마지막으로 저장된 렌더링 파라미터. Save 호출 전에는 null.
  /// </summary>
  DieRenderingParameters? CurrentParameters { get; }

  /// <summary>
  /// 주어진 파라미터로 Die Image를 <paramref name="context"/>에 그린다.
  /// </summary>
  void Render(DrawingContext context, DieRenderingParameters parameters);

  /// <summary>
  /// <paramref name="parameters"/>를 서비스 내부에 저장한다.
  /// 이후 Export 등에서 <see cref="CurrentParameters"/>로 재사용할 수 있다.
  /// </summary>
  void Save(DieRenderingParameters parameters);
}
