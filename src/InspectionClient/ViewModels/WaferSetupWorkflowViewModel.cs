using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Core.Enums;
using Core.Logging.Interfaces;
using InspectionClient.Interfaces;
using InspectionClient.Models;

namespace InspectionClient.ViewModels;

/// <summary>
/// Wafer Setup 워크플로 ViewModel.
///
/// 책임:
///   - WaferInfo CRUD (목록 로드, 생성, 저장, 삭제)
///   - LoadedItem을 직접 바인딩하여 View에서 편집
///   - 보조편집패널: ComboBox 대체 리스트 및 DieParameters 선택
/// </summary>
public partial class WaferSetupWorkflowViewModel : DbTableWorkflowViewModelBase<WaferInfoRow>
{
  private readonly IDieRenderingParametersRepository _dieRepository;

  // ── Die 파라미터 목록 ─────────────────────────────────────────────────

  public ObservableCollection<DieParametersRow> DieItems { get; } = new();

  // ── 보조편집패널 상태 ─────────────────────────────────────────────────

  public enum SidePanelMode { None, WaferType, WaferGrade, NotchOrientation, DieParameters }

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(IsSidePanelVisible))]
  [NotifyPropertyChangedFor(nameof(IsWaferTypePanel))]
  [NotifyPropertyChangedFor(nameof(IsWaferGradePanel))]
  [NotifyPropertyChangedFor(nameof(IsNotchOrientationPanel))]
  [NotifyPropertyChangedFor(nameof(IsDieParametersPanel))]
  private SidePanelMode _activeSidePanel = SidePanelMode.None;

  public bool IsSidePanelVisible      => ActiveSidePanel != SidePanelMode.None;
  public bool IsWaferTypePanel        => ActiveSidePanel == SidePanelMode.WaferType;
  public bool IsWaferGradePanel       => ActiveSidePanel == SidePanelMode.WaferGrade;
  public bool IsNotchOrientationPanel => ActiveSidePanel == SidePanelMode.NotchOrientation;
  public bool IsDieParametersPanel    => ActiveSidePanel == SidePanelMode.DieParameters;


  public WaferSetupWorkflowViewModel(
      IWaferInfoRepository              repository,
      IDieRenderingParametersRepository dieRepository,
      ILogService                       logService)
      : base(repository, logService)
  {
    _dieRepository = dieRepository;
    _ = InitializeAsync();
  }

  // ── 초기화 ───────────────────────────────────────────────────────────

  private async Task InitializeAsync()
  {
    await RefreshDieAsync();
    await RefreshAsync();
  }

  // ── Die 목록 커맨드 ─────────────────────────────────────────────────

  [RelayCommand]
  private async Task RefreshDieAsync() => await Execute(async () =>
  {
    var list = await _dieRepository.ListAsync();
    DieItems.Clear();
    foreach (var item in list)
      DieItems.Add(item);
  }, nameof(RefreshDieAsync));

  // ── 보조편집패널 커맨드 ──────────────────────────────────────────────

  [RelayCommand]
  private void OpenWaferTypePanel() =>
      ActiveSidePanel = ActiveSidePanel == SidePanelMode.WaferType
          ? SidePanelMode.None
          : SidePanelMode.WaferType;

  [RelayCommand]
  private void OpenWaferGradePanel() =>
      ActiveSidePanel = ActiveSidePanel == SidePanelMode.WaferGrade
          ? SidePanelMode.None
          : SidePanelMode.WaferGrade;

  [RelayCommand]
  private void OpenNotchOrientationPanel() =>
      ActiveSidePanel = ActiveSidePanel == SidePanelMode.NotchOrientation
          ? SidePanelMode.None
          : SidePanelMode.NotchOrientation;

  [RelayCommand]
  private void OpenDieParametersPanel() =>
      ActiveSidePanel = ActiveSidePanel == SidePanelMode.DieParameters
          ? SidePanelMode.None
          : SidePanelMode.DieParameters;

  [RelayCommand]
  private void SelectWaferType(WaferType value)
  {
    if (LoadedItem is { } row)
      row.WaferType = value;
    ActiveSidePanel = SidePanelMode.None;
  }

  [RelayCommand]
  private void SelectWaferGrade(WaferGrade value)
  {
    if (LoadedItem is { } row)
      row.WaferGrade = value;
    ActiveSidePanel = SidePanelMode.None;
  }

  [RelayCommand]
  private void SelectNotchOrientation(NotchOrientation value)
  {
    if (LoadedItem is { } row)
      row.NotchOrientation = value;
    ActiveSidePanel = SidePanelMode.None;
  }

  [RelayCommand]
  private void SelectDieParameters(DieParametersRow row)
  {
    if (LoadedItem is { } loaded)
    {
      loaded.DieParametersId = row.Id;
      loaded.DieSizeWidthUm  = row.Parameters.CanvasWidth;
      loaded.DieSizeHeightUm = row.Parameters.CanvasHeight;
    }
    ActiveSidePanel = SidePanelMode.None;
  }

  // ── 내부 헬퍼 ─────────────────────────────────────────────────────────

  private DieParametersRow? FindDieById(long id)
  {
    foreach (var item in DieItems)
      if (item.Id == id)
        return item;
    return null;
  }
}
