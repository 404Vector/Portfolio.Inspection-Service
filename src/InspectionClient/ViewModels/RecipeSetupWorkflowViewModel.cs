using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Core.Logging.Interfaces;
using Core.Models;
using InspectionClient.Interfaces;
using InspectionClient.Models;
using InspectionRecipe.Models;

namespace InspectionClient.ViewModels;

/// <summary>
/// Recipe Setup 워크플로 ViewModel.
///
/// 책임:
///   - Recipe CRUD (목록 로드, 생성, 저장, 삭제)
///   - WaferInfo 목록에서 Recipe에 연결할 WaferInfo 선택
///   - Recipe 파라미터 편집 (편집 버퍼)
///   - Load 시 편집 버퍼를 채움
///   - Save 시 편집 버퍼에서 새 RecipeRow를 조립하여 저장
/// </summary>
public partial class RecipeSetupWorkflowViewModel : ViewModelBase
{
  private readonly IRecipeRepository      _repository;
  private readonly IEquipmentConfigService _equipmentConfig;

  // ── Recipe 목록 ───────────────────────────────────────────────────────

  public ObservableCollection<RecipeRow> Items { get; } = new();

  [ObservableProperty]
  [NotifyCanExecuteChangedFor(nameof(LoadCommand))]
  [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
  private RecipeRow? _selectedItem;

  /// <summary>
  /// 현재 편집 중인 항목. DbTableControl.LoadedItem과 양방향 바인딩.
  /// null이면 Browse 상태, non-null이면 Edit 상태.
  /// </summary>
  [ObservableProperty] private RecipeRow? _loadedItem;

  // ── WaferInfo 목록 (선택 전용) ────────────────────────────────────────

  public WaferInfoListViewModel WaferTableVm { get; }

  // ── 선택된 WaferInfo ──────────────────────────────────────────────────

  [ObservableProperty] private string _selectedWaferId = string.Empty;
  [ObservableProperty] private string _waferSummary    = "(없음)";

  // ── Recipe 파라미터 편집 버퍼 ─────────────────────────────────────────

  [ObservableProperty] private string _recipeName      = "NewRecipe";
  [ObservableProperty] private string _description     = string.Empty;
  [ObservableProperty] private double _fovWidthUm      = 1413.0;
  [ObservableProperty] private double _fovHeightUm     = 1035.0;
  [ObservableProperty] private double _overlapXum      = 0.0;
  [ObservableProperty] private double _overlapYum      = 0.0;
  [ObservableProperty] private bool   _stopOnFirstFail = false;
  [ObservableProperty] private int    _maxFrameCount   = 0;

  public RecipeSetupWorkflowViewModel(
      IRecipeRepository       recipeRepository,
      IWaferInfoRepository    waferRepository,
      IEquipmentConfigService equipmentConfig,
      ILogService             logService)
      : base(logService)
  {
    _repository      = recipeRepository;
    _equipmentConfig = equipmentConfig;
    WaferTableVm     = new WaferInfoListViewModel(waferRepository, logService);
    WaferTableVm.WaferSelected += OnWaferSelected;
    _ = RefreshAsync();
    RecalculateFovFromEquipment();
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
    if (SelectedItem is not RecipeRow row)
      return;
    SetSelectedWafer(row.Recipe.WaferId, waferSummary: null);
    ApplyToForm(row.Recipe);
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
    if (SelectedItem is not RecipeRow current)
      return;
    await _repository.DeleteAsync(current.Id);
    Items.Remove(current);
    SelectedItem = null;
  }, nameof(DeleteAsync));

  [RelayCommand]
  private async Task SaveAsync() => await Execute(async () =>
  {
    if (LoadedItem is not RecipeRow current)
      return;
    current.Recipe = BuildRecipe();
    await _repository.UpdateAsync(current);
    // DbTableControl이 Save 클릭 시 LoadedItem을 null로 초기화한다.
  }, nameof(SaveAsync));

  [RelayCommand]
  private async Task CancelAsync() => await Execute(async () =>
  {
    if (LoadedItem is not RecipeRow current)
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

  // ── Wafer 선택 커맨드 ────────────────────────────────────────────────

  [RelayCommand(CanExecute = nameof(HasSelectedWaferRow))]
  private void SelectWaferInfo() => Execute(() =>
  {
    if (WaferTableVm.SelectedItem is WaferInfoRow row)
      SetSelectedWafer(row.WaferId, $"{row.WaferId} | {row.WaferType} | Die {row.DieSizeWidthUm:0}×{row.DieSizeHeightUm:0} µm");
  }, nameof(SelectWaferInfo));

  private bool HasSelectedWaferRow => WaferTableVm.SelectedItem is not null;

  // ── FOV 계산 커맨드 ──────────────────────────────────────────────────

  [RelayCommand]
  private void RecalculateFovFromEquipment() => Execute(() =>
  {
    var cfg     = _equipmentConfig.Config;
    double eff  = cfg.EffectivePixelSizeUm;
    FovWidthUm  = cfg.SensorWidth  * eff;
    FovHeightUm = cfg.SensorHeight * eff;
  }, nameof(RecalculateFovFromEquipment));

  // ── 이벤트 핸들러 ────────────────────────────────────────────────────

  private void OnWaferSelected(object? sender, WaferInfoRow row) =>
      SetSelectedWafer(row.WaferId, $"{row.WaferId} | {row.WaferType} | Die {row.DieSizeWidthUm:0}×{row.DieSizeHeightUm:0} µm");

  // ── CanExecute 헬퍼 ─────────────────────────────────────────────────

  private bool HasSelectedItem => SelectedItem is not null;

  // ── 내부 헬퍼 ─────────────────────────────────────────────────────────

  private void SetSelectedWafer(string waferId, string? waferSummary)
  {
    SelectedWaferId = waferId;
    WaferSummary    = waferSummary ?? (string.IsNullOrEmpty(waferId) ? "(없음)" : waferId);
  }

  private WaferSurfaceInspectionRecipe BuildRecipe() => new(
    RecipeName:      RecipeName,
    Description:     Description,
    WaferId:         SelectedWaferId,
    Fov:             new FovSize(FovWidthUm, FovHeightUm),
    OverlapXum:      OverlapXum,
    OverlapYum:      OverlapYum,
    StopOnFirstFail: StopOnFirstFail,
    MaxFrameCount:   MaxFrameCount
  );

  private void ApplyToForm(WaferSurfaceInspectionRecipe recipe)
  {
    RecipeName      = recipe.RecipeName;
    Description     = recipe.Description;
    SetSelectedWafer(recipe.WaferId, waferSummary: null);
    FovWidthUm      = recipe.Fov.WidthUm;
    FovHeightUm     = recipe.Fov.HeightUm;
    OverlapXum      = recipe.OverlapXum;
    OverlapYum      = recipe.OverlapYum;
    StopOnFirstFail = recipe.StopOnFirstFail;
    MaxFrameCount   = recipe.MaxFrameCount;
  }

  private RecipeRow? FindById(long id)
  {
    foreach (var item in Items)
      if (item.Id == id)
        return item;
    return null;
  }
}
