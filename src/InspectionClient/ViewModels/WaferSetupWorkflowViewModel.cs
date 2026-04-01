using System;
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
public partial class WaferSetupWorkflowViewModel : ViewModelBase
{
  private readonly IWaferInfoRepository _repository;
  private readonly IDieRenderingParametersRepository _dieRepository;

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

  /// <summary>현재 선택된 DieParametersRow (DieSize 표시용).</summary>
  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(SelectedDieParametersLabel))]
  private DieParametersRow? _selectedDieParameters;

  public string SelectedDieParametersLabel =>
      SelectedDieParameters is { } p
          ? $"{p.Name} - {p.Parameters.CanvasWidth:0}×{p.Parameters.CanvasHeight:0}"
          : "(선택 없음)";

  public WaferSetupWorkflowViewModel(
      IWaferInfoRepository              repository,
      IDieRenderingParametersRepository dieRepository,
      ILogService                       logService)
      : base(logService)
  {
    _repository    = repository;
    _dieRepository = dieRepository;
    _ = InitializeAsync();
  }

  // ── 초기화 ───────────────────────────────────────────────────────────

  private async Task InitializeAsync()
  {
    await RefreshDieAsync();
    await RefreshAsync();
  }

  // ── 커맨드 ───────────────────────────────────────────────────────────

  [RelayCommand]
  private async Task RefreshDieAsync() => await Execute(async () =>
  {
    var list = await _dieRepository.ListAsync();
    DieItems.Clear();
    foreach (var item in list)
      DieItems.Add(item);
  }, nameof(RefreshDieAsync));

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
    if (item is not WaferInfoRow row)
      return;
    SelectedDieParameters = row.DieParametersId is { } id ? FindDieById(id) : null;
    // DbTableControl이 LoadedItem을 SelectedItem으로 set한다.
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
    current.DieParametersId = SelectedDieParameters?.Id;
    await _repository.UpdateAsync(current);
    LoadedItem = null;
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
    LoadedItem = null;
  }, nameof(CancelAsync));

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
    SelectedDieParameters = row;
    if (LoadedItem is { } loaded)
    {
      loaded.DieSizeWidthUm  = row.Parameters.CanvasWidth;
      loaded.DieSizeHeightUm = row.Parameters.CanvasHeight;
    }
    ActiveSidePanel = SidePanelMode.None;
  }

  // ── CanExecute 헬퍼 ─────────────────────────────────────────────────

  private bool HasSelectedItem => SelectedItem is not null;

  // ── 내부 헬퍼 ─────────────────────────────────────────────────────────

  private WaferInfoRow? FindById(long id)
  {
    foreach (var item in Items)
      if (item.Id == id)
        return item;
    return null;
  }

  private DieParametersRow? FindDieById(long id)
  {
    foreach (var item in DieItems)
      if (item.Id == id)
        return item;
    return null;
  }
}
