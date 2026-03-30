using System.Globalization;
using Avalonia;
using Avalonia.Media;
using InspectionClient.Interfaces;
using InspectionClient.Models;

namespace InspectionClient.Services;

/// <summary>
/// Test Die Image를 Avalonia DrawingContext에 렌더링한다.
/// 각 요소(얼라인먼트 마크, 눈금자, 패드 행, 밴드, 캘리브레이션 마크)의
/// 그리기 책임을 전담한다.
/// </summary>
public sealed class DieImageRenderer : IDieImageRenderer
{
  /// <inheritdoc />
  public DieRenderingParameters? CurrentParameters { get; private set; }

  /// <inheritdoc />
  public void Save(DieRenderingParameters parameters)
  {
    // 호출 시점의 값을 snapshot으로 보존한다.
    // 원본 인스턴스를 그대로 저장하면 이후 ViewModel의 수정이 반영되어
    // CanApply() 비교가 항상 false를 반환하게 된다.
    if (CurrentParameters is null)
      CurrentParameters = new DieRenderingParameters();

    CurrentParameters.CopyFrom(parameters);
  }

  /// <inheritdoc />
  public void Render(DrawingContext context, DieRenderingParameters p)
  {
    var bgBrush   = new SolidColorBrush(Color.FromRgb(p.BackgroundGray, p.BackgroundGray, p.BackgroundGray));
    var white     = Brushes.White;
    var lightGray = new SolidColorBrush(Color.FromRgb(180, 180, 180));

    // 1. 배경
    context.FillRectangle(bgBrush, new Rect(0, 0, p.CanvasWidth, p.CanvasHeight));

    // 2. 얼라인먼트 마크
    if (p.ShowAlignmentMarks)
      DrawAlignmentMarks(context, white, p.CanvasWidth, p.CanvasHeight);

    // 3. 눈금자
    if (p.ShowRuler)
      DrawRuler(context, white);

    // 4. 패드 행
    int[] rowPositions = ComputeRowPositions(p.PadRowCount, p.CanvasHeight);
    foreach (int y in rowPositions)
      DrawPadRow(context, y, white, p.PadColumnCount);

    // 5. 텍스처 밴드
    if (p.ShowTextureBands)
    {
      context.FillRectangle(lightGray, new Rect(100, 320, 1000, 80));
      context.FillRectangle(lightGray, new Rect(100, 570, 1000, 80));
    }

    // 6. 캘리브레이션 마크
    if (p.ShowCalibrationMark)
      DrawCalibrationMark(context, white);
  }

  // ── 얼라인먼트 마크 (L자) ────────────────────────────────────────────────

  private static void DrawAlignmentMarks(DrawingContext ctx, IBrush brush, int w, int h)
  {
    const int Margin    = 30;
    const int Length    = 60;
    const int Thickness = 10;
    var pen = new Pen(brush, Thickness);

    // 좌측 상단
    ctx.DrawLine(pen, new Point(Margin, Margin), new Point(Margin + Length, Margin));
    ctx.DrawLine(pen, new Point(Margin, Margin), new Point(Margin, Margin + Length));

    // 우측 하단
    ctx.DrawLine(pen, new Point(w - Margin, h - Margin), new Point(w - Margin - Length, h - Margin));
    ctx.DrawLine(pen, new Point(w - Margin, h - Margin), new Point(w - Margin, h - Margin - Length));

    // 우측 상단
    ctx.DrawLine(pen, new Point(w - Margin, Margin), new Point(w - Margin - Length, Margin));
    ctx.DrawLine(pen, new Point(w - Margin, Margin), new Point(w - Margin, Margin + Length));

    // 좌측 하단
    ctx.DrawLine(pen, new Point(Margin, h - Margin), new Point(Margin + Length, h - Margin));
    ctx.DrawLine(pen, new Point(Margin, h - Margin), new Point(Margin, h - Margin - Length));
  }

  // ── 상단 눈금자 ─────────────────────────────────────────────────────────

  private static void DrawRuler(DrawingContext ctx, IBrush brush)
  {
    const int RulerY = 100;
    var pen = new Pen(brush, 2);
    var typeface = new Typeface("Inter,Arial,sans-serif");

    for (int i = 0; i <= 15; i++)
    {
      int x = 150 + i * 60;
      ctx.DrawLine(pen, new Point(x, RulerY), new Point(x, RulerY - 20));

      var ft = new FormattedText(
        $"{i} mm",
        CultureInfo.InvariantCulture,
        FlowDirection.LeftToRight,
        typeface,
        10,
        brush);
      ctx.DrawText(ft, new Point(x + 3, RulerY - 18));
    }
  }

  // ── 패드 행 (빗살무늬) ───────────────────────────────────────────────────

  private static void DrawPadRow(DrawingContext ctx, int startY, IBrush brush, int columns)
  {
    const int StartX    = 150;
    const int PadWidth  = 80;
    const int Gap       = 20;
    const int CombStep  = 8;
    const int CombDepth = 50;

    var boxPen  = new Pen(brush, 2);
    var combPen = new Pen(brush, 1);

    for (int i = 0; i < columns; i++)
    {
      int x = StartX + i * (PadWidth + Gap);

      // 상단 박스
      ctx.DrawRectangle(null, boxPen, new Rect(x, startY, PadWidth, 20));

      // 빗살 라인
      for (int lx = x + 5; lx < x + PadWidth; lx += CombStep)
        ctx.DrawLine(combPen, new Point(lx, startY + 20), new Point(lx, startY + 20 + CombDepth));
    }
  }

  // ── 캘리브레이션 마크 ────────────────────────────────────────────────────

  private static void DrawCalibrationMark(DrawingContext ctx, IBrush brush)
  {
    ctx.DrawEllipse(brush, null, new Point(120, 800), 10, 10);

    var ft = new FormattedText(
      "CALIBRATION MARK",
      CultureInfo.InvariantCulture,
      FlowDirection.LeftToRight,
      new Typeface("Inter,Arial,sans-serif"),
      11,
      brush);
    ctx.DrawText(ft, new Point(140, 792));
  }

  // ── 헬퍼: 패드 행 Y 위치 계산 ───────────────────────────────────────────

  private static int[] ComputeRowPositions(int rowCount, int canvasHeight)
  {
    // 고정 4행 위치를 기본값으로 사용하고, rowCount에 맞게 균등 분배
    if (rowCount <= 0) return [];

    int topMargin    = 200;
    int bottomMargin = canvasHeight - 150;
    int span         = bottomMargin - topMargin;

    var positions = new int[rowCount];
    for (int i = 0; i < rowCount; i++)
      positions[i] = rowCount == 1
        ? topMargin
        : topMargin + span * i / (rowCount - 1);

    return positions;
  }
}
