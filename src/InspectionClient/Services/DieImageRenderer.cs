using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using InspectionClient.Interfaces;
using InspectionClient.Models;

namespace InspectionClient.Services;

/// <summary>
/// Test Die Image를 Avalonia DrawingContext에 렌더링한다.
///
/// 단위 규약:
///   CanvasWidth / CanvasHeight 의 단위는 μm(마이크로미터)이며,
///   Avalonia 픽셀 좌표와 1:1로 매핑된다 (1 px = 1 μm).
///   기본 캔버스 10,000 × 10,000 μm = 10 mm × 10 mm Die.
///
/// 구역(Zone) 레이아웃 (Y 방향, 위→아래):
///   [0       ~ H*0.04)  Margin        — 얼라인먼트 마크 전용
///   [H*0.04 ~ H*0.11)  Ruler zone    — 눈금자                  ( 7%)
///   [H*0.12 ~ H*0.19)  Pad zone      — Bond Pad                ( 7%)
///   [H*0.20 ~ H*0.47)  Resolution V  — 수직 Line Pair 차트     (27%)
///   [H*0.48 ~ H*0.75)  Resolution H  — 수평 Line Pair 차트     (27%)
///   [H*0.76 ~ H*0.89)  Texture/Gray  — 텍스처 밴드 + 그레이 램프 (13%)
///   [H*0.90 ~ H*1.00)  Calibration   — 캘리브레이션 마크       (10%)
/// </summary>
public sealed class DieImageRenderer : IDieImageRenderer
{
  /// <inheritdoc />
  public DieRenderingParameters? CurrentParameters { get; private set; }

  /// <inheritdoc />
  public void Save(DieRenderingParameters parameters)
  {
    if (CurrentParameters is null)
      CurrentParameters = new DieRenderingParameters();

    CurrentParameters.CopyFrom(parameters);
  }

  /// <inheritdoc />
  public void Render(DrawingContext context, DieRenderingParameters p)
  {
    int w = p.CanvasWidth;
    int h = p.CanvasHeight;

    var bgBrush   = new SolidColorBrush(Color.FromRgb(p.BackgroundGray, p.BackgroundGray, p.BackgroundGray));
    var white     = Brushes.White;
    var lightGray = new SolidColorBrush(Color.FromRgb(180, 180, 180));

    // 1. 배경
    context.FillRectangle(bgBrush, new Rect(0, 0, w, h));

    // ── 구역 경계 계산 ─────────────────────────────────────────────────
    // 비율로 정의하여 캔버스 크기가 달라져도 레이아웃이 유지된다.
    int rulerTop      = (int)(h * 0.04);
    int rulerBottom   = (int)(h * 0.11);
    int padTop        = (int)(h * 0.12);
    int padBottom     = (int)(h * 0.19);
    int resVTop       = (int)(h * 0.20);
    int resVBottom    = (int)(h * 0.47);
    int resHTop       = (int)(h * 0.48);
    int resHBottom    = (int)(h * 0.75);
    int textureTop    = (int)(h * 0.76);
    int textureBottom = (int)(h * 0.89);
    int calTop        = (int)(h * 0.90);

    // 2. 얼라인먼트 마크
    if (p.ShowAlignmentMarks)
      DrawAlignmentMarks(context, white, w, h);

    // 3. 눈금자
    if (p.ShowRuler)
      DrawRuler(context, white, w, rulerTop, rulerBottom);

    // 4. 패드 행
    double padZoneInnerH = (padBottom - padTop) * 0.80;
    double padHeight     = padZoneInnerH / p.PadRowCount / 1.60;
    int[]  rowYPositions = ComputeRowPositions(p.PadRowCount, padTop, padBottom);
    foreach (int y in rowYPositions)
      DrawPadRow(context, w, y, padHeight, white, bgBrush, p.PadColumnCount);

    // 5. 해상도 차트 — 수직 Line Pair
    DrawResolutionChart(context, w, resVTop, resVBottom, white, lightGray, vertical: true);

    // 6. 해상도 차트 — 수평 Line Pair
    DrawResolutionChart(context, w, resHTop, resHBottom, white, lightGray, vertical: false);

    // 7. 텍스처 밴드 + 그레이 램프
    if (p.ShowTextureBands)
      DrawTextureAndGrayRamp(context, w, textureTop, textureBottom, white, lightGray);

    // 8. 캘리브레이션 마크
    if (p.ShowCalibrationMark)
      DrawCalibrationMark(context, w, h, calTop, white);
  }

  // ── 얼라인먼트 마크 (L자, 4코너) ────────────────────────────────────────

  private static void DrawAlignmentMarks(DrawingContext ctx, IBrush brush, int w, int h)
  {
    int margin    = Math.Max(10, (int)(Math.Min(w, h) * 0.020));
    int armLength = Math.Max(20, (int)(Math.Min(w, h) * 0.040));
    int thickness = Math.Max(4,  (int)(Math.Min(w, h) * 0.008));
    var pen = new Pen(brush, thickness);

    DrawLMark(ctx, pen, margin,     margin,     armLength, false, false);
    DrawLMark(ctx, pen, w - margin, margin,     armLength, true,  false);
    DrawLMark(ctx, pen, margin,     h - margin, armLength, false, true);
    DrawLMark(ctx, pen, w - margin, h - margin, armLength, true,  true);
  }

  private static void DrawLMark(
      DrawingContext ctx, Pen pen,
      int x, int y, int arm,
      bool flipX, bool flipY)
  {
    int dx = flipX ? -arm : arm;
    int dy = flipY ? -arm : arm;

    ctx.DrawLine(pen, new Point(x, y), new Point(x + dx, y));
    ctx.DrawLine(pen, new Point(x, y), new Point(x, y + dy));
  }

  // ── 눈금자 (μm 눈금) ─────────────────────────────────────────────────────

  private static void DrawRuler(
      DrawingContext ctx, IBrush brush, int canvasWidth, int rulerTop, int rulerBottom)
  {
    int leftMargin  = (int)(canvasWidth * 0.05);
    int rightEdge   = (int)(canvasWidth * 0.95);
    int rulerWidth  = rightEdge - leftMargin;

    int majorStepUm = ComputeRulerStep(canvasWidth);
    int tickCount   = canvasWidth / majorStepUm;

    int    baseY       = rulerTop + (int)((rulerBottom - rulerTop) * 0.6);
    int    majorHeight = (int)((rulerBottom - rulerTop) * 0.5);
    int    minorHeight = majorHeight / 2;

    var    pen      = new Pen(brush, Math.Max(1, (int)(canvasWidth * 0.001)));
    var    typeface = new Typeface("Inter,Arial,sans-serif");
    double fontSize = Math.Max(80, canvasWidth * 0.008);

    // 기준선
    ctx.DrawLine(pen, new Point(leftMargin, baseY), new Point(rightEdge, baseY));

    // 눈금 간격 레이블 — 첫 번째 구간 중앙에 1회 표시
    string stepLabel = majorStepUm >= 1000
        ? $"/{majorStepUm / 1000.0:0.#} mm"
        : $"/{majorStepUm} μm";
    var stepFt = new FormattedText(
        stepLabel, CultureInfo.InvariantCulture,
        FlowDirection.LeftToRight, typeface, fontSize, brush);
    double firstTickX = leftMargin + rulerWidth / (double)tickCount;
    ctx.DrawText(stepFt, new Point(
        leftMargin + (firstTickX - leftMargin) / 2 - stepFt.Width / 2,
        baseY - majorHeight - fontSize * 1.1));

    for (int i = 0; i <= tickCount; i++)
    {
      int x = leftMargin + (int)(rulerWidth * (double)i / tickCount);

      ctx.DrawLine(pen, new Point(x, baseY), new Point(x, baseY - majorHeight));

      if (i < tickCount)
      {
        for (int sub = 1; sub < 4; sub++)
        {
          int sx = leftMargin + (int)(rulerWidth * (i + sub / 4.0) / tickCount);
          ctx.DrawLine(pen, new Point(sx, baseY), new Point(sx, baseY - minorHeight));
        }
      }
    }
  }

  private static int ComputeRulerStep(int totalUm)
  {
    int[] candidates = [100, 200, 500, 1000, 2000, 5000, 10_000];
    foreach (int step in candidates)
    {
      if (totalUm / step <= 12)
        return step;
    }
    return candidates[^1];
  }

  // ── 패드 행 ──────────────────────────────────────────────────────────────
  //
  // Bond Pad 패턴. 목적:
  //   - 패드 중심 위치 검출 → X/Y 정렬 오프셋(Overlay) 측정
  //   - 직각 모서리의 Edge Response 측정
  //   - 패드 간 피치로 거리 측정 캘리브레이션
  //   - Vernier 스케일로 서브픽셀 오프셋 시각적 확인
  //
  // 구성:
  //   [Vernier 상단] 피치 P 라인 패턴
  //   [패드 본체]    채워진 직사각형 + 음각 십자선(중심 명시)
  //   [Vernier 하단] 피치 P+δ 라인 패턴 (δ = 1 μm)
  //
  // centerY는 패드 행의 수직 중심. zone 내부 여백 안에서 전체 구성이 완결된다.

  private static void DrawPadRow(
      DrawingContext ctx, int canvasWidth, int centerY,
      double padHeight,
      IBrush padBrush, IBrush bgBrush, int columns)
  {
    double usableWidth = canvasWidth * 0.80;
    double leftMargin  = canvasWidth * 0.10;

    double totalGap  = usableWidth * 0.20;
    double padWidth  = (usableWidth - totalGap) / columns;
    double gap       = totalGap / (columns + 1);
    // padHeight는 호출부에서 zone 높이 기반으로 계산되어 전달된다.
    double topY      = centerY - padHeight / 2;

    // Vernier 설정
    double vernierH    = padHeight * 0.30;
    double vernierTopY = topY - vernierH;
    double vernierBotY = topY + padHeight;
    // 기준 피치: 패드 폭의 10%
    double pitchBase   = Math.Max(2, padWidth * 0.10);
    double pitchShift  = 1.0;   // 하단 Vernier 피치 오프셋 (1 μm)
    double lineW       = pitchBase * 0.4;
    var    vernierPen  = new Pen(padBrush, Math.Max(1, (int)(canvasWidth * 0.0008)));

    double crossThick  = Math.Max(2, padHeight * 0.08);
    var    crossPen    = new Pen(bgBrush, crossThick);

    for (int i = 0; i < columns; i++)
    {
      double x = leftMargin + gap * (i + 1) + padWidth * i;

      ctx.FillRectangle(padBrush, new Rect(x, topY, padWidth, padHeight));

      double cx = x + padWidth  / 2;
      double cy = topY + padHeight / 2;
      ctx.DrawLine(crossPen,
          new Point(cx, topY + crossThick),
          new Point(cx, topY + padHeight - crossThick));
      ctx.DrawLine(crossPen,
          new Point(x + crossThick, cy),
          new Point(x + padWidth - crossThick, cy));

      for (double lx = x; lx + lineW <= x + padWidth; lx += pitchBase)
        ctx.DrawLine(vernierPen,
            new Point(lx, vernierTopY),
            new Point(lx, vernierTopY + vernierH));

      for (double lx = x; lx + lineW <= x + padWidth; lx += pitchBase + pitchShift)
        ctx.DrawLine(vernierPen,
            new Point(lx, vernierBotY),
            new Point(lx, vernierBotY + vernierH));
    }
  }

  // ── 해상도 차트 (Line Pairs) ──────────────────────────────────────────────
  //
  // vertical=true : 수직 라인 (X방향 해상도 평가)
  // vertical=false: 수평 라인 (Y방향 해상도 평가)

  private static void DrawResolutionChart(
      DrawingContext ctx, int canvasWidth,
      int zoneTop, int zoneBottom,
      IBrush brightBrush, IBrush midBrush,
      bool vertical)
  {
    int    zoneHeight  = zoneBottom - zoneTop;
    int    leftMargin  = (int)(canvasWidth * 0.05);
    int    rightEdge   = (int)(canvasWidth * 0.95);
    int    usableW     = rightEdge - leftMargin;
    int    usableH     = (int)(zoneHeight * 0.80);
    int    patternsTop = zoneTop + (int)(zoneHeight * 0.10);

    int[]  lineWidthsUm = ComputeLinePairWidths(canvasWidth);
    int    groupCount   = lineWidthsUm.Length;
    double groupW       = (double)usableW / groupCount;
    double innerMargin  = groupW * 0.06;

    var    typeface  = new Typeface("Inter,Arial,sans-serif");
    double labelSize = Math.Max(60, canvasWidth * 0.006);

    string header = vertical ? "V Line Pairs (X resolution)" : "H Line Pairs (Y resolution)";
    var headerFt = new FormattedText(
        header, CultureInfo.InvariantCulture,
        FlowDirection.LeftToRight, typeface, labelSize * 1.3, midBrush);
    ctx.DrawText(headerFt, new Point(leftMargin, zoneTop));

    for (int g = 0; g < groupCount; g++)
    {
      double gx    = leftMargin + groupW * g + innerMargin;
      double gwUse = groupW - innerMargin * 2;
      int    lw    = lineWidthsUm[g];
      double pitch = lw * 2.0;

      if (vertical)
      {
        for (double x = gx; x + lw <= gx + gwUse; x += pitch)
          ctx.FillRectangle(brightBrush, new Rect(x, patternsTop, lw, usableH));
      }
      else
      {
        for (double y = patternsTop; y + lw <= patternsTop + usableH; y += pitch)
          ctx.FillRectangle(brightBrush, new Rect(gx, y, gwUse, lw));
      }

      string label = lw >= 1000
          ? $"{lw / 1000.0:0.#}mm"
          : $"{lw}μm";
      var ft = new FormattedText(
          label, CultureInfo.InvariantCulture,
          FlowDirection.LeftToRight, typeface, labelSize, midBrush);
      ctx.DrawText(ft, new Point(gx, patternsTop + usableH + labelSize * 0.2));
    }
  }

  private static int[] ComputeLinePairWidths(int canvasWidthUm)
  {
    int coarsest = Math.Max(2, (int)(canvasWidthUm * 0.05));
    coarsest = RoundToNice(coarsest);

    var result  = new List<int>();
    int current = coarsest;
    while (current >= 1 && result.Count < 8)
    {
      result.Add(current);
      current = current / 2;
    }
    if (result[^1] > 1)
      result.Add(1);

    return [.. result];
  }

  private static int RoundToNice(int v)
  {
    int[] nice = [1, 2, 5, 10, 20, 50, 100, 200, 500, 1000, 2000, 5000];
    int best = nice[0];
    foreach (int n in nice)
    {
      if (Math.Abs(n - v) < Math.Abs(best - v))
        best = n;
    }
    return best;
  }

  // ── 텍스처 밴드 + 그레이 스케일 램프 ────────────────────────────────────

  private static void DrawTextureAndGrayRamp(
      DrawingContext ctx, int canvasWidth,
      int zoneTop, int zoneBottom,
      IBrush whiteBrush, IBrush lightGrayBrush)
  {
    int zoneHeight = zoneBottom - zoneTop;
    int leftMargin = (int)(canvasWidth * 0.05);
    int rightEdge  = (int)(canvasWidth * 0.95);
    int usableW    = rightEdge - leftMargin;

    int bandHeight = (int)(zoneHeight * 0.15);
    int band1Y     = zoneTop + (int)(zoneHeight * 0.03);
    int band2Y     = band1Y + bandHeight + (int)(zoneHeight * 0.05);

    ctx.FillRectangle(lightGrayBrush, new Rect(leftMargin, band1Y, usableW, bandHeight));
    ctx.FillRectangle(lightGrayBrush, new Rect(leftMargin, band2Y, usableW, bandHeight));

    int rampTop    = band2Y + bandHeight + (int)(zoneHeight * 0.05);
    int rampHeight = (int)(zoneHeight * 0.25);
    int stepW      = usableW / 8;
    int rampBottom = rampTop + rampHeight;

    var    typeface = new Typeface("Inter,Arial,sans-serif");
    double lblSz    = Math.Max(60, canvasWidth * 0.006);

    for (int i = 0; i < 8; i++)
    {
      byte grayLevel = (byte)(255 * i / 7);
      var  fill      = new SolidColorBrush(Color.FromRgb(grayLevel, grayLevel, grayLevel));
      int  sx        = leftMargin + stepW * i;

      ctx.FillRectangle(fill, new Rect(sx, rampTop, stepW, rampHeight));
      ctx.DrawRectangle((IBrush?)null,
          new Pen(whiteBrush, Math.Max(1, (int)(canvasWidth * 0.0005))),
          new Rect(sx, rampTop, stepW, rampHeight));

      var ft = new FormattedText(
          $"{grayLevel}", CultureInfo.InvariantCulture,
          FlowDirection.LeftToRight, typeface, lblSz,
          grayLevel < 128 ? whiteBrush : Brushes.Black);
      ctx.DrawText(ft, new Point(sx + stepW * 0.1, rampTop + rampHeight * 0.3));
    }

    var rampLabel = new FormattedText(
        "GRAY SCALE RAMP", CultureInfo.InvariantCulture,
        FlowDirection.LeftToRight, typeface, lblSz * 1.2, whiteBrush);
    ctx.DrawText(rampLabel, new Point(leftMargin, rampBottom + lblSz * 0.4));
  }

  // ── 캘리브레이션 마크 ────────────────────────────────────────────────────

  private static void DrawCalibrationMark(
      DrawingContext ctx, int canvasWidth, int canvasHeight,
      int zoneTop, IBrush brush)
  {
    int    cx        = canvasWidth / 2;
    int    cy        = zoneTop + (canvasHeight - zoneTop) / 2;
    int    outerR    = Math.Max(20, (int)(Math.Min(canvasWidth, canvasHeight) * 0.025));
    int    innerR    = outerR / 2;
    int    crossArm  = (int)(outerR * 1.6);
    double thickness = Math.Max(2, canvasWidth * 0.002);
    var    pen       = new Pen(brush, thickness);

    ctx.DrawEllipse((IBrush?)null, pen, new Point(cx, cy), outerR, outerR);
    ctx.DrawEllipse((IBrush?)null, pen, new Point(cx, cy), innerR, innerR);
    ctx.DrawLine(pen, new Point(cx - crossArm, cy), new Point(cx + crossArm, cy));
    ctx.DrawLine(pen, new Point(cx, cy - crossArm), new Point(cx, cy + crossArm));

    var    typeface = new Typeface("Inter,Arial,sans-serif");
    double fontSize = Math.Max(60, canvasWidth * 0.007);
    var ft = new FormattedText(
        "CALIBRATION MARK", CultureInfo.InvariantCulture,
        FlowDirection.LeftToRight, typeface, fontSize, brush);
    ctx.DrawText(ft, new Point(cx + outerR * 1.3, cy - fontSize / 2));
  }

  // ── 패드 행 Y 위치 계산 ──────────────────────────────────────────────────

  private static int[] ComputeRowPositions(int rowCount, int zoneTop, int zoneBottom)
  {
    if (rowCount <= 0) return [];

    int innerMargin = (zoneBottom - zoneTop) / 10;
    int innerTop    = zoneTop    + innerMargin;
    int innerBottom = zoneBottom - innerMargin;
    int span        = innerBottom - innerTop;

    var positions = new int[rowCount];
    for (int i = 0; i < rowCount; i++)
    {
      positions[i] = rowCount == 1
          ? innerTop + span / 2
          : innerTop + span * i / (rowCount - 1);
    }
    return positions;
  }
}
