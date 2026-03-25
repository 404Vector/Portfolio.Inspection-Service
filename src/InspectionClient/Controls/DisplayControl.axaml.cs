using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace InspectionClient.Controls;

/// <summary>
/// 검사 이미지 표시 컨트롤.
/// WriteableBitmap을 직접 교체하는 방식으로 고속 프레임 업데이트를 지원하며,
/// RenderTransform 기반 Zoom / Pan을 제공한다.
/// </summary>
public partial class DisplayControl : UserControl
{
    // ── Avalonia Properties ──────────────────────────────────────────────

    /// <summary>
    /// 표시할 비트맵. 외부에서 WriteableBitmap을 교체하면 즉시 반영된다.
    /// </summary>
    public static readonly StyledProperty<WriteableBitmap?> SourceProperty =
        AvaloniaProperty.Register<DisplayControl, WriteableBitmap?>(nameof(Source));

    public WriteableBitmap? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    // ── Zoom 설정 ────────────────────────────────────────────────────────

    private const double ZoomStep   = 1.15;   // 휠 한 칸당 배율
    private const double ZoomMin    = 0.05;   // 최소 배율  (5%)
    private const double ZoomMax    = 32.0;   // 최대 배율 (3200%)

    // ── Transform 객체 (code-behind에서 직접 보유) ───────────────────────

    private readonly MatrixTransform _matrixXform = new(Matrix.Identity);

    // ── 상태 ─────────────────────────────────────────────────────────────

    private double _scale = 1.0;
    private Vector _translate;          // Canvas 원점의 Viewport 내 오프셋
    private Point  _panStart;           // 팬 드래그 시작 포인터 위치
    private Vector _panTranslateStart;  // 팬 드래그 시작 시점의 _translate
    private bool   _isPanning;
    private PixelSize _lastFitSize;     // FitToViewport를 적용한 마지막 이미지 크기

    // ── 생성자 ───────────────────────────────────────────────────────────

    public DisplayControl()
    {
        InitializeComponent();
        Canvas.RenderTransform = _matrixXform;
    }

    // ── AvaloniaProperty 변경 감지 ───────────────────────────────────────

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SourceProperty)
            ApplySource(change.NewValue as WriteableBitmap);
    }

    /// <summary>
    /// WriteableBitmap 교체. UI 스레드 여부를 묻지 않고 항상 Render 우선순위로 포스트.
    /// 고속 프레임 교체 시 최신 비트맵만 그려지도록 Render 우선순위를 사용한다.
    /// </summary>
    private void ApplySource(WriteableBitmap? bitmap)
    {
        Dispatcher.UIThread.Post(() =>
        {
            DisplayImage.Source = bitmap;

            if (bitmap is null) return;

            // Image 크기를 비트맵 픽셀 크기로 맞춘다 (Stretch="None" 전제).
            // Canvas 자체는 Width/Height=0 으로 고정해 레이아웃 원점을 Viewport (0,0)에 유지한다.
            DisplayImage.Width  = bitmap.PixelSize.Width;
            DisplayImage.Height = bitmap.PixelSize.Height;

            // 이미지 크기가 바뀐 경우에만 FitToViewport를 적용한다.
            // 동일 크기 프레임이 계속 들어오는 경우(고정 해상도 스트리밍)에는
            // 사용자의 zoom/pan 상태를 유지한다.
            if (bitmap.PixelSize != _lastFitSize)
            {
                _lastFitSize = bitmap.PixelSize;
                FitToViewport();
            }
        }, DispatcherPriority.Render);
    }

    // ── 초기 레이아웃: 이미지가 뷰포트 중앙에 Fit으로 배치 ───────────────

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        FitToViewport();
    }

    /// <summary>
    /// 이미지를 뷰포트에 맞게 축소/확대하고 중앙에 배치한다.
    /// Source가 없으면 1:1 중앙 배치.
    /// </summary>
    public void FitToViewport()
    {
        var vw = Viewport.Bounds.Width;
        var vh = Viewport.Bounds.Height;
        if (vw <= 0 || vh <= 0) return;

        double iw = Source?.PixelSize.Width  ?? vw;
        double ih = Source?.PixelSize.Height ?? vh;

        _scale = Math.Min(vw / iw, vh / ih);
        _scale = Math.Clamp(_scale, ZoomMin, ZoomMax);

        // 중앙 정렬
        _translate = new Vector(
            (vw - iw * _scale) / 2.0,
            (vh - ih * _scale) / 2.0);

        ApplyTransform();
    }

    // ── 줌 ──────────────────────────────────────────────────────────────

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        var delta  = e.Delta.Y > 0 ? ZoomStep : 1.0 / ZoomStep;
        var pivot  = e.GetPosition(Viewport);   // 뷰포트 기준 마우스 위치
        ZoomAt(pivot, delta);
        e.Handled = true;
    }

    /// <summary>
    /// mousePos(뷰포트 좌표) 아래의 이미지 픽셀이 zoom 후에도 동일한 화면 위치에 오도록
    /// 새 배율을 적용하고 translate를 보정한다.
    ///
    /// 1. zoom 전: 마우스 아래 이미지 좌표 = (mousePos - t) / s
    /// 2. zoom 후: 같은 이미지 좌표가 mousePos에 오려면
    ///            t' = mousePos - imagePos * s'
    /// </summary>
    private void ZoomAt(Point mousePos, double factor)
    {
        var newScale = Math.Clamp(_scale * factor, ZoomMin, ZoomMax);

        // zoom 전 마우스 아래 이미지 좌표 (이미지 픽셀 공간)
        var imageX = (mousePos.X - _translate.X) / _scale;
        var imageY = (mousePos.Y - _translate.Y) / _scale;

        // zoom 후 동일 이미지 좌표가 마우스 위치에 오도록 translate 재계산
        _translate = new Vector(
            mousePos.X - imageX * newScale,
            mousePos.Y - imageY * newScale);

        _scale = newScale;
        ApplyTransform();
    }

    // ── 팬 ──────────────────────────────────────────────────────────────

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(Viewport).Properties.IsMiddleButtonPressed &&
            !e.GetCurrentPoint(Viewport).Properties.IsLeftButtonPressed) return;

        _isPanning          = true;
        _panStart           = e.GetPosition(Viewport);
        _panTranslateStart  = _translate;
        e.Pointer.Capture(Viewport);
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var pos = e.GetPosition(Viewport);

        UpdateCursorLabel(pos);

        if (!_isPanning) return;

        var delta = pos - _panStart;
        _translate = _panTranslateStart + delta;
        ApplyTransform();
        e.Handled = true;
    }

    private void UpdateCursorLabel(Point viewportPos)
    {
        if (Source is null)
        {
            CursorLabel.Text = string.Empty;
            return;
        }

        var ix = (int)Math.Floor((viewportPos.X - _translate.X) / _scale);
        var iy = (int)Math.Floor((viewportPos.Y - _translate.Y) / _scale);

        // 이미지 범위 밖이면 표시하지 않는다
        if (ix < 0 || iy < 0 || ix >= Source.PixelSize.Width || iy >= Source.PixelSize.Height)
        {
            CursorLabel.Text = string.Empty;
            return;
        }

        CursorLabel.Text = $"({ix}, {iy})";
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isPanning) return;
        _isPanning = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void OnPointerExited(object? _, PointerEventArgs __)
        => CursorLabel.Text = string.Empty;

    // ── 편의 버튼 ────────────────────────────────────────────────────────

    private void OnFitClicked(object? _, Avalonia.Interactivity.RoutedEventArgs __)
        => FitToViewport();

    private void OnZoomInClicked(object? _, Avalonia.Interactivity.RoutedEventArgs __)
    {
        var center = new Point(Viewport.Bounds.Width / 2, Viewport.Bounds.Height / 2);
        ZoomAt(center, ZoomStep);
    }

    private void OnZoomOutClicked(object? _, Avalonia.Interactivity.RoutedEventArgs __)
    {
        var center = new Point(Viewport.Bounds.Width / 2, Viewport.Bounds.Height / 2);
        ZoomAt(center, 1.0 / ZoomStep);
    }

    // ── Transform / Label 반영 ───────────────────────────────────────────

    private void ApplyTransform()
    {
        // Matrix: Scale(s) 후 Translate(tx, ty)
        // | s  0  tx |
        // | 0  s  ty |
        // | 0  0   1 |
        _matrixXform.Matrix = new Matrix(
            _scale, 0,
            0,      _scale,
            _translate.X, _translate.Y);

        ZoomLabel.Text = $"{_scale * 100:F0}%";
    }
}
