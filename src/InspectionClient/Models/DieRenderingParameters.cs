using CommunityToolkit.Mvvm.ComponentModel;

namespace InspectionClient.Models;

/// <summary>
/// Test Die Image 렌더링 파라미터.
/// ViewModel이 단독 소유하고 View가 직접 바인딩하므로 ObservableObject로 구현한다.
/// </summary>
public partial class DieRenderingParameters : ObservableObject
{
  // ── 범위 상수 ──────────────────────────────────────────────────────────
  public static class Limits
  {
    public const int CanvasWidthMin    = 100;
    public const int CanvasWidthMax    = 4096;
    public const int CanvasHeightMin   = 100;
    public const int CanvasHeightMax   = 4096;
    public const int BackgroundGrayMin = 0;
    public const int BackgroundGrayMax = 255;
    public const int PadRowCountMin    = 1;
    public const int PadRowCountMax    = 8;
    public const int PadColumnCountMin = 1;
    public const int PadColumnCountMax = 20;
  }

  [ObservableProperty] private int  _canvasWidth         = 1200;
  [ObservableProperty] private int  _canvasHeight        = 1000;

  /// <summary>0–255. 배경 그레이 레벨.</summary>
  [ObservableProperty] private byte _backgroundGray      = 70;

  /// <summary>얼라인먼트 마크 그리기 여부.</summary>
  [ObservableProperty] private bool _showAlignmentMarks  = true;

  /// <summary>상단 눈금자 그리기 여부.</summary>
  [ObservableProperty] private bool _showRuler           = true;

  /// <summary>중간 텍스처 밴드 그리기 여부.</summary>
  [ObservableProperty] private bool _showTextureBands    = true;

  /// <summary>캘리브레이션 마크 그리기 여부.</summary>
  [ObservableProperty] private bool _showCalibrationMark = true;

  /// <summary>패드 행 개수 (1–8).</summary>
  [ObservableProperty] private int  _padRowCount         = 4;

  /// <summary>패드 행당 열 개수 (1–20).</summary>
  [ObservableProperty] private int  _padColumnCount      = 10;

  /// <summary>
  /// 다른 파라미터 인스턴스와 모든 필드 값이 동일한지 비교한다.
  /// </summary>
  public bool ValueEquals(DieRenderingParameters other) =>
      CanvasWidth         == other.CanvasWidth
   && CanvasHeight        == other.CanvasHeight
   && BackgroundGray      == other.BackgroundGray
   && ShowAlignmentMarks  == other.ShowAlignmentMarks
   && ShowRuler           == other.ShowRuler
   && ShowTextureBands    == other.ShowTextureBands
   && ShowCalibrationMark == other.ShowCalibrationMark
   && PadRowCount         == other.PadRowCount
   && PadColumnCount      == other.PadColumnCount;

  /// <summary>
  /// 다른 파라미터 인스턴스의 값을 이 인스턴스에 복사한다.
  /// </summary>
  public void CopyFrom(DieRenderingParameters source)
  {
    CanvasWidth         = source.CanvasWidth;
    CanvasHeight        = source.CanvasHeight;
    BackgroundGray      = source.BackgroundGray;
    ShowAlignmentMarks  = source.ShowAlignmentMarks;
    ShowRuler           = source.ShowRuler;
    ShowTextureBands    = source.ShowTextureBands;
    ShowCalibrationMark = source.ShowCalibrationMark;
    PadRowCount         = source.PadRowCount;
    PadColumnCount      = source.PadColumnCount;
  }
}
