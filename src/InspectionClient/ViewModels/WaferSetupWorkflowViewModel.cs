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
///   - WaferInfo CRUD (목록 로드, 생성, 저장, 삭제)
///   - WaferInfo 입력 폼 바인딩 (편집 버퍼)
///   - Load 시 편집 버퍼를 채움
///   - Save 시 편집 버퍼에서 새 WaferInfo record를 조립하여 저장
///   - Die 파라미터 목록에서 DieSize 가져오기
/// </summary>
public partial class WaferSetupWorkflowViewModel : ViewModelBase
{
  private readonly IWaferInfoRepository _repository;

  // ── WaferInfo 목록 ────────────────────────────────────────────────────

  public ObservableCollection<WaferInfoRow> Items { get; } = new();

  [ObservableProperty]
  [NotifyCanExecuteChangedFor(nameof(LoadCommand))]
  [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
  private WaferInfoRow? _selectedItem;

  /// <summary>
  /// 현재 편집 중인 항목. DbTableControl.LoadedItem과 양방향 바인딩.
  /// null이면 Browse 상태, non-null이면 Edit 상태.
  /// </summary>
  [ObservableProperty] private WaferInfoRow? _loadedItem;

  // ── Die 파라미터 목록 (DieSize import 전용) ──────────────────────────

  public DieParametersListViewModel DieTableVm { get; }

  // ── 입력 폼 바인딩 (편집 버퍼) ──────────────────────────────────────────

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

  public WaferSetupWorkflowViewModel(
      IWaferInfoRepository              repository,
      IDieRenderingParametersRepository dieRepository,
      ILogService                       logService)
      : base(logService)
  {
    _repository = repository;
    DieTableVm  = new DieParametersListViewModel(dieRepository, logService);
    _ = RefreshAsync();
  }

  // ── CRUD 커맨드 ──────────────────────────────────────────────────────

  [RelayCommand]
  private async Task RefreshAsync() => await Execute(async () =>
  {
    var list = await _repository.ListAsync();
    Items.Clear();
    foreach (var item in list)
      Items.Add(item);
  }, nameof(RefreshAsync));

  [RelayCommand(CanExecute = nameof(HasSelectedItem))]
  private void Load(object? item) => Execute(() =>
  {
    if (SelectedItem is not WaferInfoRow row)
      return;
    ApplyToForm(row.Info);
    // DbTableControl이 LoadedItem을 SelectedItem으로 set한다.
    // ViewModel은 TwoWay 바인딩으로 동기화만 수행한다.
  }, nameof(Load));

  [RelayCommand]
  private async Task CreateAsync() => await Execute(async () =>
  {
    var name = $"New-{DateTime.Now:yyMMdd-HHmmss}";
    var row  = await _repository.CreateAsync(name);
    await RefreshAsync();
    SelectedItem = FindById(row.Id);
  }, nameof(CreateAsync));

  [RelayCommand(CanExecute = nameof(HasSelectedItem))]
  private async Task DeleteAsync() => await Execute(async () =>
  {
    if (SelectedItem is not WaferInfoRow current)
      return;
    await _repository.DeleteAsync(current.Id);
    Items.Remove(current);
    SelectedItem = null;
  }, nameof(DeleteAsync));

  [RelayCommand]
  private async Task SaveAsync() => await Execute(async () =>
  {
    if (LoadedItem is not WaferInfoRow current)
      return;
    var updated = current with { Info = BuildWaferInfo() };
    await _repository.UpdateAsync(updated);
    var idx = Items.IndexOf(current);
    if (idx >= 0)
      Items[idx] = updated;
    SelectedItem = updated;
    // DbTableControl이 Save 클릭 시 LoadedItem을 null로 초기화한다.
  }, nameof(SaveAsync));

  [RelayCommand]
  private async Task CancelAsync() => await Execute(async () =>
  {
    if (LoadedItem is not WaferInfoRow current)
      return;
    var restored = await _repository.FindByIdAsync(current.Id);
    if (restored is not null)
    {
      var idx = Items.IndexOf(current);
      if (idx >= 0)
        Items[idx] = restored;
      SelectedItem = restored;
    }
    // DbTableControl이 Cancel 클릭 시 LoadedItem을 null로 초기화한다.
  }, nameof(CancelAsync));

  // ── Die Import 커맨드 ─────────────────────────────────────────────────

  [RelayCommand(CanExecute = nameof(HasSelectedDieEntry))]
  private void ImportDieSize() => Execute(() =>
  {
    var p = DieTableVm.SelectedItem!.Parameters;
    DieSizeWidthUm  = (decimal)p.CanvasWidth;
    DieSizeHeightUm = (decimal)p.CanvasHeight;
  }, nameof(ImportDieSize));

  private bool HasSelectedDieEntry => DieTableVm.SelectedItem is not null;

  // ── CanExecute 헬퍼 ─────────────────────────────────────────────────

  private bool HasSelectedItem => SelectedItem is not null;

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

  private WaferInfoRow? FindById(long id)
  {
    foreach (var item in Items)
      if (item.Id == id)
        return item;
    return null;
  }
}
