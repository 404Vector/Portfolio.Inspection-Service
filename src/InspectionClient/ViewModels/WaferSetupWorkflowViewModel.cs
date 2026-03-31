using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Core.Enums;
using Core.Logging.Interfaces;
using Core.Models;
using InspectionClient.Interfaces;
using InspectionClient.Models;

namespace InspectionClient.ViewModels;

/// <summary>
/// Wafer Setup 워크플로 ViewModel.
///
/// 책임:
///   - WaferInfo 입력 폼 바인딩
///   - DieSetupWorkflow에서 DieSize 가져오기
///   - Save: IWaferInfoRepository에 저장
///   - Load: DB 목록에서 선택하여 폼에 채우기
///   - Delete: 선택된 WaferInfo를 DB에서 삭제
/// </summary>
public partial class WaferSetupWorkflowViewModel : ViewModelBase
{
  private readonly IWaferInfoRepository                _repository;
  private readonly IDieRenderingParametersRepository   _dieRepository;

  // ── 입력 폼 바인딩 프로퍼티 ─────────────────────────────────────────────

  [ObservableProperty] private string           _waferId          = "WAFER-001";
  [ObservableProperty] private string           _lotId            = "LOT-001";
  [ObservableProperty] private int              _slotIndex        = 1;
  [ObservableProperty] private WaferType        _waferType        = WaferType.Wafer300mm;
  [ObservableProperty] private decimal?         _thicknessUm      = 775m;
  [ObservableProperty] private WaferGrade       _waferGrade       = WaferGrade.Dummy;
  [ObservableProperty] private NotchOrientation _notchOrientation = NotchOrientation.Down;
  [ObservableProperty] private decimal?         _dieSizeWidthUm   = 10_000m;
  [ObservableProperty] private decimal?         _dieSizeHeightUm  = 10_000m;
  [ObservableProperty] private decimal?         _dieOffsetXum     = 0m;
  [ObservableProperty] private decimal?         _dieOffsetYum     = 0m;
  [ObservableProperty] private decimal?         _waferOffsetXum   = 0m;
  [ObservableProperty] private decimal?         _waferOffsetYum   = 0m;
  [ObservableProperty] private string           _processStep      = "Unknown";

  /// <summary>마지막으로 저장된 WaferInfo 요약. 저장 전에는 "(미저장)".</summary>
  [ObservableProperty] private string _savedSummary = "(미저장)";

  // ── WaferInfo 목록 패널 ─────────────────────────────────────────────────

  public ObservableCollection<WaferInfo> WaferInfoList { get; } = new();

  [ObservableProperty]
  [NotifyCanExecuteChangedFor(nameof(LoadSelectedCommand))]
  [NotifyCanExecuteChangedFor(nameof(DeleteSelectedCommand))]
  private WaferInfo? _selectedWaferInfo;

  // ── Die 파라미터 목록 패널 (DieSize 가져오기용) ──────────────────────────

  public ObservableCollection<DieParametersEntry> DieParameterList { get; } = new();

  [ObservableProperty]
  [NotifyCanExecuteChangedFor(nameof(ImportDieSizeCommand))]
  private DieParametersEntry? _selectedDieEntry;

  public WaferSetupWorkflowViewModel(
      IWaferInfoRepository               repository,
      IDieRenderingParametersRepository  dieRepository,
      ILogService                        logService)
      : base(logService)
  {
    _repository    = repository;
    _dieRepository = dieRepository;
  }

  // ── 커맨드 ─────────────────────────────────────────────────────────────

  /// <summary>DB에서 Die 파라미터 목록을 새로고침한다.</summary>
  [RelayCommand]
  private async Task RefreshDieListAsync() => await Execute(async () =>
  {
    var entries = await _dieRepository.ListAsync();
    DieParameterList.Clear();
    foreach (var entry in entries)
      DieParameterList.Add(entry);
  }, nameof(RefreshDieListAsync));

  /// <summary>선택된 Die 파라미터의 CanvasSize를 DieSizeWidth/Height에 적용한다.</summary>
  [RelayCommand(CanExecute = nameof(HasSelectedDieEntry))]
  private void ImportDieSize() => Execute(() =>
  {
    var p = SelectedDieEntry!.Parameters;
    DieSizeWidthUm  = (decimal)p.CanvasWidth;
    DieSizeHeightUm = (decimal)p.CanvasHeight;
  }, nameof(ImportDieSize));

  private bool HasSelectedDieEntry => SelectedDieEntry is not null;

  /// <summary>현재 입력값으로 WaferInfo를 생성하고 Repository에 저장한다.</summary>
  [RelayCommand]
  private async Task SaveAsync() => await Execute(async () =>
  {
    var waferInfo = BuildWaferInfo();
    await _repository.SaveAsync(waferInfo);
    SavedSummary = BuildSummary(waferInfo);
    await RefreshListAsync();
  }, nameof(SaveAsync));

  /// <summary>DB에서 WaferInfo 목록을 새로고침한다.</summary>
  [RelayCommand]
  private async Task RefreshListAsync() => await Execute(async () =>
  {
    var list = await _repository.ListAsync();
    WaferInfoList.Clear();
    foreach (var item in list)
      WaferInfoList.Add(item);
  }, nameof(RefreshListAsync));

  /// <summary>선택된 WaferInfo를 폼에 채운다.</summary>
  [RelayCommand(CanExecute = nameof(HasSelectedWaferInfo))]
  private void LoadSelected() => Execute(() =>
  {
    ApplyToForm(SelectedWaferInfo!);
    SavedSummary = BuildSummary(SelectedWaferInfo!);
  }, nameof(LoadSelected));

  /// <summary>선택된 WaferInfo를 DB에서 삭제한다.</summary>
  [RelayCommand(CanExecute = nameof(HasSelectedWaferInfo))]
  private async Task DeleteSelectedAsync() => await Execute(async () =>
  {
    var toDelete = SelectedWaferInfo!;
    await _repository.DeleteAsync(toDelete.WaferId);
    SelectedWaferInfo = null;
    await RefreshListAsync();
  }, nameof(DeleteSelectedAsync));

  // ── CanExecute 헬퍼 ───────────────────────────────────────────────────

  private bool HasSelectedWaferInfo => SelectedWaferInfo is not null;

  // ── 내부 헬퍼 ─────────────────────────────────────────────────────────

  private WaferInfo BuildWaferInfo() => new(
    WaferId:          WaferId,
    LotId:            LotId,
    SlotIndex:        SlotIndex,
    WaferType:        WaferType,
    ThicknessUm:      (double)(ThicknessUm ?? 775m),
    Grade:            WaferGrade,
    NotchOrientation: NotchOrientation,
    CoordinateOrigin: WaferCoordinate.Origin,
    DieSize:          new DieSize((double)(DieSizeWidthUm ?? 10_000m), (double)(DieSizeHeightUm ?? 10_000m)),
    DieOffset:        new WaferCoordinate((double)(DieOffsetXum ?? 0m), (double)(DieOffsetYum ?? 0m)),
    WaferOffset:      new WaferCoordinate((double)(WaferOffsetXum ?? 0m), (double)(WaferOffsetYum ?? 0m)),
    ProcessStep:      ProcessStep,
    CreatedAt:        DateTimeOffset.UtcNow
  );

  private void ApplyToForm(WaferInfo info)
  {
    WaferId          = info.WaferId;
    LotId            = info.LotId;
    SlotIndex        = info.SlotIndex;
    WaferType        = info.WaferType;
    ThicknessUm      = (decimal)info.ThicknessUm;
    WaferGrade       = info.Grade;
    NotchOrientation = info.NotchOrientation;
    DieSizeWidthUm   = (decimal)info.DieSize.WidthUm;
    DieSizeHeightUm  = (decimal)info.DieSize.HeightUm;
    DieOffsetXum     = (decimal)info.DieOffset.Xum;
    DieOffsetYum     = (decimal)info.DieOffset.Yum;
    WaferOffsetXum   = (decimal)info.WaferOffset.Xum;
    WaferOffsetYum   = (decimal)info.WaferOffset.Yum;
    ProcessStep      = info.ProcessStep;
  }

  private static string BuildSummary(WaferInfo info) =>
      $"{info.WaferId} | {info.WaferType} | Die {info.DieSize.WidthUm:0}×{info.DieSize.HeightUm:0} µm";
}
