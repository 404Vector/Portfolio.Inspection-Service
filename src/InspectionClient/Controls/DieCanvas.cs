using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using InspectionClient.Interfaces;
using InspectionClient.Models;

namespace InspectionClient.Controls;

/// <summary>
/// IDieImageRenderer.Render를 OnRender에서 호출하는 순수 렌더링 캔버스.
/// DieRenderingControl 내부에서만 사용된다.
/// </summary>
public sealed class DieCanvas : Control
{
  public IDieImageRenderer? Renderer   { get; set; }
  public DieRenderingParameters? Parameters { get; set; }

  public override void Render(DrawingContext context)
  {
    if (Renderer is null || Parameters is null) return;
    Renderer.Render(context, Parameters);
  }
}
