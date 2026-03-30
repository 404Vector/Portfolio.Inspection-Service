using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using InspectionClient.Interfaces;
using InspectionClient.Models;

namespace InspectionClient.Controls;

/// <summary>
/// Test Die Image를 표시하는 컨트롤.
/// IDieImageRenderer를 통해 렌더링하며, DisplayControl과 동일한
/// MatrixTransform 기반 Zoom / Pan을 지원한다.
/// </summary>
public partial class DieRenderingControl : UserControl
{
  // ── Avalonia Properties ──────────────────────────────────────────────

  public static readonly StyledProperty<IDieImageRenderer?> RendererProperty =
      AvaloniaProperty.Register<DieRenderingControl, IDieImageRenderer?>(nameof(Renderer));

  public static readonly StyledProperty<DieRenderingParameters?> ParametersProperty =
      AvaloniaProperty.Register<DieRenderingControl, DieRenderingParameters?>(nameof(Parameters));

  public IDieImageRenderer? Renderer
  {
    get => GetValue(RendererProperty);
    set => SetValue(RendererProperty, value);
  }

  public DieRenderingParameters? Parameters
  {
    get => GetValue(ParametersProperty);
    set => SetValue(ParametersProperty, value);
  }

  // ── Zoom 설정 ────────────────────────────────────────────────────────

  private const double ZoomStep = 1.15;
  private const double ZoomMin  = 0.05;
  private const double ZoomMax  = 32.0;

  // ── Transform ────────────────────────────────────────────────────────

  private readonly MatrixTransform _matrixXform = new(Matrix.Identity);

  // ── 상태 ─────────────────────────────────────────────────────────────

  private double _scale = 1.0;
  private Vector _translate;
  private Point  _panStart;
  private Vector _panTranslateStart;
  private bool   _isPanning;

  // ── 생성자 ───────────────────────────────────────────────────────────

  public DieRenderingControl()
  {
    InitializeComponent();
    Canvas.RenderTransform = _matrixXform;
  }

  // ── Avalonia Property 변경 감지 ──────────────────────────────────────

  protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
  {
    base.OnPropertyChanged(change);

    if (change.Property == RendererProperty)
    {
      Refresh();
    }
    else if (change.Property == ParametersProperty)
    {
      // 이전 인스턴스의 구독 해제 후 새 인스턴스 구독
      if (change.OldValue is DieRenderingParameters old)
        old.PropertyChanged -= OnParametersPropertyChanged;

      if (change.NewValue is DieRenderingParameters next)
        next.PropertyChanged += OnParametersPropertyChanged;

      Refresh();
    }
  }

  /// <summary>
  /// Parameters 인스턴스 내부 프로퍼티가 바뀌면 재그리기를 요청한다.
  /// CanvasWidth/Height 변경 시에는 FitToViewport도 함께 수행한다.
  /// </summary>
  private void OnParametersPropertyChanged(object? sender, PropertyChangedEventArgs e)
  {
    if (e.PropertyName is nameof(DieRenderingParameters.CanvasWidth)
                       or nameof(DieRenderingParameters.CanvasHeight))
    {
      Refresh();
    }
    else
    {
      DieCanvas.InvalidateVisual();
    }
  }

  /// <summary>
  /// 렌더러 또는 캔버스 크기가 바뀔 때 DieCanvas 크기를 갱신하고 재그리기를 요청한다.
  /// </summary>
  private void Refresh()
  {
    if (Parameters is null) return;

    DieCanvas.Renderer   = Renderer;
    DieCanvas.Parameters = Parameters;
    DieCanvas.Width      = Parameters.CanvasWidth;
    DieCanvas.Height     = Parameters.CanvasHeight;
    DieCanvas.InvalidateVisual();
    FitToViewport();
  }

  // ── 크기 변경 시 자동 Fit ────────────────────────────────────────────

  protected override void OnSizeChanged(SizeChangedEventArgs e)
  {
    base.OnSizeChanged(e);
    FitToViewport();
  }

  public void FitToViewport()
  {
    var vw = Viewport.Bounds.Width;
    var vh = Viewport.Bounds.Height;
    if (vw <= 0 || vh <= 0) return;

    double iw = Parameters?.CanvasWidth  ?? vw;
    double ih = Parameters?.CanvasHeight ?? vh;

    _scale = Math.Clamp(Math.Min(vw / iw, vh / ih), ZoomMin, ZoomMax);
    _translate = new Vector(
        (vw - iw * _scale) / 2.0,
        (vh - ih * _scale) / 2.0);

    ApplyTransform();
  }

  // ── 줌 ──────────────────────────────────────────────────────────────

  private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
  {
    var factor = e.Delta.Y > 0 ? ZoomStep : 1.0 / ZoomStep;
    ZoomAt(e.GetPosition(Viewport), factor);
    e.Handled = true;
  }

  private void ZoomAt(Point mousePos, double factor)
  {
    var newScale = Math.Clamp(_scale * factor, ZoomMin, ZoomMax);
    var imageX   = (mousePos.X - _translate.X) / _scale;
    var imageY   = (mousePos.Y - _translate.Y) / _scale;

    _translate = new Vector(
        mousePos.X - imageX * newScale,
        mousePos.Y - imageY * newScale);
    _scale = newScale;
    ApplyTransform();
  }

  // ── 팬 ──────────────────────────────────────────────────────────────

  private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
  {
    var props = e.GetCurrentPoint(Viewport).Properties;
    if (!props.IsMiddleButtonPressed && !props.IsLeftButtonPressed) return;

    _isPanning         = true;
    _panStart          = e.GetPosition(Viewport);
    _panTranslateStart = _translate;
    e.Pointer.Capture(Viewport);
    e.Handled = true;
  }

  private void OnPointerMoved(object? sender, PointerEventArgs e)
  {
    if (!_isPanning) return;
    _translate = _panTranslateStart + (e.GetPosition(Viewport) - _panStart);
    ApplyTransform();
    e.Handled = true;
  }

  private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
  {
    if (!_isPanning) return;
    _isPanning = false;
    e.Pointer.Capture(null);
    e.Handled = true;
  }

  private void OnPointerExited(object? sender, PointerEventArgs e) { }

  // ── 편의 버튼 ────────────────────────────────────────────────────────

  private void OnFitClicked(object? _, Avalonia.Interactivity.RoutedEventArgs __)
      => FitToViewport();

  private void OnZoomInClicked(object? _, Avalonia.Interactivity.RoutedEventArgs __)
      => ZoomAt(new Point(Viewport.Bounds.Width / 2, Viewport.Bounds.Height / 2), ZoomStep);

  private void OnZoomOutClicked(object? _, Avalonia.Interactivity.RoutedEventArgs __)
      => ZoomAt(new Point(Viewport.Bounds.Width / 2, Viewport.Bounds.Height / 2), 1.0 / ZoomStep);

  // ── Transform 반영 ───────────────────────────────────────────────────

  private void ApplyTransform()
  {
    _matrixXform.Matrix = new Matrix(
        _scale, 0,
        0,      _scale,
        _translate.X, _translate.Y);

    ZoomLabel.Text = $"{_scale * 100:F0}%";
  }
}
