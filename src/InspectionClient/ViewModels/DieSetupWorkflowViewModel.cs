using CommunityToolkit.Mvvm.ComponentModel;
using Core.Logging.Interfaces;
using InspectionClient.Interfaces;
using InspectionClient.Models;

namespace InspectionClient.ViewModels;

/// <summary>
/// Die Setup 워크플로 ViewModel.
///
/// 책임:
///   - DieRenderingParameters CRUD (목록 로드, 생성, 저장, 삭제)
///   - 선택 항목을 DieRenderingControl에 연결
/// </summary>
public sealed partial class DieSetupWorkflowViewModel : DbTableWorkflowViewModelBase<DieParametersRow>
{
  public IDieImageRenderer Renderer { get; }

  // ── 파라미터 범위 (View NumericUpDown 바인딩용) ───────────────────────

  public int CanvasWidthMin    => DieRenderingParameters.Limits.CanvasWidthMin;
  public int CanvasWidthMax    => DieRenderingParameters.Limits.CanvasWidthMax;
  public int CanvasHeightMin   => DieRenderingParameters.Limits.CanvasHeightMin;
  public int CanvasHeightMax   => DieRenderingParameters.Limits.CanvasHeightMax;
  public int BackgroundGrayMin => DieRenderingParameters.Limits.BackgroundGrayMin;
  public int BackgroundGrayMax => DieRenderingParameters.Limits.BackgroundGrayMax;
  public int PadRowCountMin    => DieRenderingParameters.Limits.PadRowCountMin;
  public int PadRowCountMax    => DieRenderingParameters.Limits.PadRowCountMax;
  public int PadColumnCountMin => DieRenderingParameters.Limits.PadColumnCountMin;
  public int PadColumnCountMax => DieRenderingParameters.Limits.PadColumnCountMax;

  /// <summary>
  /// 현재 편집 중인 파라미터. LoadedItem이 없으면 기본값 인스턴스를 반환한다.
  /// WaferSetupWorkflow에서 DieSize 가져오기에 사용된다.
  /// </summary>
  public DieRenderingParameters Parameters =>
      LoadedItem?.Parameters ?? _defaultParameters;

  private readonly DieRenderingParameters _defaultParameters = new();

  public DieSetupWorkflowViewModel(
      IDieRenderingParametersRepository repository,
      IDieImageRenderer                 renderer,
      ILogService                       logService)
      : base(repository, logService)
  {
    Renderer = renderer;
    _ = RefreshAsync();
  }

  // ── LoadedItem 변경 시 Parameters 알림 ───────────────────────────────

  protected override void OnLoadedItemUpdated(DieParametersRow? value) =>
      OnPropertyChanged(nameof(Parameters));
}
