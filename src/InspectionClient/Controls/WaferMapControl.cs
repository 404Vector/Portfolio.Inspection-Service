using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Core.Models;
using InspectionClient.Models;

namespace InspectionClient.Controls;

/// <summary>
/// 웨이퍼 맵을 렌더링하는 순수 UI 컴포넌트.
///
/// DieMap을 받아 웨이퍼 원과 Die 격자를 그리고,
/// DieStatuses 딕셔너리를 통해 각 Die의 검사 상태를 색상으로 표시한다.
///
/// 단위: 내부 좌표는 µm. Render 시 Bounds에 맞게 uniform scale 적용.
/// </summary>
public sealed class WaferMapControl : Control
{
  // ── Avalonia StyledProperties ──────────────────────────────────────────

  public static readonly StyledProperty<DieMap?> DieMapProperty =
      AvaloniaProperty.Register<WaferMapControl, DieMap?>(nameof(DieMap));

  public static readonly StyledProperty<IReadOnlyDictionary<DieIndex, DieInspectionState>?> DieStatusesProperty =
      AvaloniaProperty.Register<WaferMapControl, IReadOnlyDictionary<DieIndex, DieInspectionState>?>(nameof(DieStatuses));

  public DieMap? DieMap
  {
    get => GetValue(DieMapProperty);
    set => SetValue(DieMapProperty, value);
  }

  public IReadOnlyDictionary<DieIndex, DieInspectionState>? DieStatuses
  {
    get => GetValue(DieStatusesProperty);
    set => SetValue(DieStatusesProperty, value);
  }

  static WaferMapControl()
  {
    // 프로퍼티 변경 시 재렌더링
    DieMapProperty.Changed.AddClassHandler<WaferMapControl>((c, _) => c.InvalidateVisual());
    DieStatusesProperty.Changed.AddClassHandler<WaferMapControl>((c, _) => c.InvalidateVisual());
    AffectsRender<WaferMapControl>(DieMapProperty, DieStatusesProperty);
  }

  // ── 렌더링 ────────────────────────────────────────────────────────────

  public override void Render(DrawingContext context)
  {
    var map = DieMap;
    if (map is null) return;

    double radiusUm = map.RadiusUm;
    double mapSpan  = radiusUm * 2.0;

    double availW = Bounds.Width;
    double availH = Bounds.Height;
    double scale  = System.Math.Min(availW, availH) / mapSpan * 0.92; // 4% margin each side

    double cx = availW / 2.0;
    double cy = availH / 2.0;

    // µm → pixel 변환: 웨이퍼 중심을 (cx, cy)로 매핑, Y 반전 (Avalonia Y 아래 방향)
    Point ToScreen(double xum, double yum) =>
        new(cx + xum * scale, cy - yum * scale);

    // 1. 웨이퍼 원
    double screenRadius = radiusUm * scale;
    context.DrawEllipse(
        new SolidColorBrush(Color.FromArgb(30, 100, 149, 237)),
        new Pen(new SolidColorBrush(Color.FromRgb(100, 149, 237)), 1.5),
        new Point(cx, cy), screenRadius, screenRadius);

    // 2. Die 격자
    var statuses  = DieStatuses;
    var diePen    = new Pen(new SolidColorBrush(Color.FromArgb(80, 150, 150, 150)), 0.5);

    foreach (var die in map.Dies)
    {
      double x0 = die.BottomLeft.Xum;
      double y0 = die.BottomLeft.Yum;
      double x1 = x0 + die.WidthUm;
      double y1 = y0 + die.HeightUm;

      var tl = ToScreen(x0, y1);  // top-left (화면 기준)
      double w = (x1 - x0) * scale;
      double h = (y1 - y0) * scale;

      var rect   = new Rect(tl.X, tl.Y, w, h);
      var state  = statuses is not null && statuses.TryGetValue(die.Index, out var s)
          ? s
          : DieInspectionState.Pending;

      var fill = state switch
      {
        DieInspectionState.Current => new SolidColorBrush(Color.FromArgb(180, 255, 200, 0)),
        DieInspectionState.Pass    => new SolidColorBrush(Color.FromArgb(160, 50,  200, 100)),
        DieInspectionState.Fail    => new SolidColorBrush(Color.FromArgb(180, 220, 60,  60)),
        _                          => new SolidColorBrush(Color.FromArgb(40,  180, 180, 200)),
      };

      context.FillRectangle(fill, rect);
      context.DrawRectangle(null, diePen, rect);
    }

    // 3. 중심 십자선
    var crossPen = new Pen(new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)), 0.5);
    context.DrawLine(crossPen, new Point(cx - 8, cy), new Point(cx + 8, cy));
    context.DrawLine(crossPen, new Point(cx, cy - 8), new Point(cx, cy + 8));
  }
}
