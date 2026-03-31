using CommunityToolkit.Mvvm.ComponentModel;

namespace InspectionClient.Models;

/// <summary>
/// Test Die Image 렌더링 파라미터.
/// ViewModel이 단독 소유하고 View가 직접 바인딩하므로 ObservableObject로 구현한다.
///
/// 단위 규약: CanvasWidth / CanvasHeight의 단위는 μm(마이크로미터)이며,
/// 렌더링 시 1 픽셀 = 1 μm 로 매핑한다.
/// 예) 기본값 10,000 μm = 10 mm × 10 mm Die.
/// </summary>
public partial class DieRenderingParameters : ObservableObject
{
  // ── 범위 상수 ──────────────────────────────────────────────────────────
  public static class Limits
  {
    /// <summary>최소 캔버스 폭 (μm). 1,000 μm = 1 mm.</summary>
    public const int CanvasWidthMin    = 3_000;
    /// <summary>
    /// 최대 캔버스 폭/높이 (μm).
    /// 웨이퍼 직경 200 mm 기준, 엣지 제외 반경 (100 - 2) mm를
    /// 대각선 방향 Die 최대 변으로 환산: (98 mm) / √2 ≈ 69,296 μm.
    /// </summary>
    public const int CanvasWidthMax    = 69_296;
    /// <summary>최소 캔버스 높이 (μm).</summary>
    public const int CanvasHeightMin   = 3_000;
    /// <summary>최대 캔버스 높이 (μm). <see cref="CanvasWidthMax"/> 참조.</summary>
    public const int CanvasHeightMax   = 69_296;
    public const int BackgroundGrayMin = 0;
    public const int BackgroundGrayMax = 255;
    public const int PadRowCountMin    = 1;
    /// <summary>
    /// 패드 행 최대 2. Overlay/Edge Response 측정에는 1~2행으로 충분하며,
    /// 그 이상은 중복 정보로 Resolution 차트 영역만 축소한다.
    /// </summary>
    public const int PadRowCountMax    = 2;
    public const int PadColumnCountMin = 1;
    /// <summary>패드 열 최대 8. 통계적 신뢰를 위한 최소 샘플 수 확보 수준.</summary>
    public const int PadColumnCountMax = 8;
  }

  /// <summary>캔버스 폭 (단위: μm). 기본값 10,000 μm = 10 mm.</summary>
  [ObservableProperty] private int  _canvasWidth         = 10_000;
  /// <summary>캔버스 높이 (단위: μm). 기본값 10,000 μm = 10 mm.</summary>
  [ObservableProperty] private int  _canvasHeight        = 10_000;

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

  /// <summary>패드 행 개수 (1–2).</summary>
  [ObservableProperty] private int  _padRowCount         = 1;

  /// <summary>패드 행당 열 개수 (1–8).</summary>
  [ObservableProperty] private int  _padColumnCount      = 6;

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
