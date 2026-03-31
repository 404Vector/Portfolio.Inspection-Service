using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Core.Logging.Interfaces;
using Core.Models;
using InspectionClient.Interfaces;
using InspectionRecipe.Models;

namespace InspectionClient.ViewModels;

/// <summary>
/// Recipe Setup 워크플로 ViewModel.
///
/// 책임:
///   - WaferInfo를 IWaferInfoRepository에서 로드
///   - EquipmentConfig 기반 FOV 자동 계산
///   - Recipe 파라미터 편집
///   - Save: IRecipeRepository에 저장
///   - Load: DB 목록에서 선택하여 폼에 채우기
///   - Delete: 선택된 Recipe를 DB에서 삭제
/// </summary>
public partial class RecipeSetupWorkflowViewModel : ViewModelBase
{
  private readonly IRecipeRepository       _recipeRepository;
  private readonly IWaferInfoRepository    _waferRepository;
  private readonly IEquipmentConfigService _equipmentConfig;

  // ── WaferInfo (로드된 것) ────────────────────────────────────────────

  private WaferInfo? _loadedWaferInfo;

  /// <summary>현재 연결된 WaferInfo 요약. 없으면 "(없음)".</summary>
  [ObservableProperty] private string _waferSummary = "(없음)";

  /// <summary>WaferInfo가 로드되었는지 여부 (Save/Confirm 버튼 CanExecute 기준).</summary>
  [ObservableProperty]
  [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
  private bool _hasWaferInfo;

  // ── Recipe 파라미터 ────────────────────────────────────────────────────

  [ObservableProperty] private string  _recipeName      = "NewRecipe";
  [ObservableProperty] private string  _description     = string.Empty;
  [ObservableProperty] private double  _fovWidthUm      = 1413.0;
  [ObservableProperty] private double  _fovHeightUm     = 1035.0;
  [ObservableProperty] private double  _overlapXum      = 0.0;
  [ObservableProperty] private double  _overlapYum      = 0.0;
  [ObservableProperty] private bool    _stopOnFirstFail = false;
  [ObservableProperty] private int     _maxFrameCount   = 0;

  /// <summary>마지막으로 저장된 Recipe 이름. 저장 전에는 "(미저장)".</summary>
  [ObservableProperty] private string _savedRecipeName = "(미저장)";

  // ── 목록 패널 ───────────────────────────────────────────────────────────

  public ObservableCollection<WaferSurfaceInspectionRecipe> RecipeList { get; } = new();

  [ObservableProperty]
  [NotifyCanExecuteChangedFor(nameof(LoadSelectedCommand))]
  [NotifyCanExecuteChangedFor(nameof(DeleteSelectedCommand))]
  private WaferSurfaceInspectionRecipe? _selectedRecipe;

  // ── WaferInfo 목록 패널 ─────────────────────────────────────────────────

  public ObservableCollection<WaferInfo> WaferInfoList { get; } = new();

  [ObservableProperty]
  [NotifyCanExecuteChangedFor(nameof(SelectWaferInfoCommand))]
  private WaferInfo? _selectedWaferInfo;

  public RecipeSetupWorkflowViewModel(
      IRecipeRepository       recipeRepository,
      IWaferInfoRepository    waferRepository,
      IEquipmentConfigService equipmentConfig,
      ILogService             logService)
      : base(logService)
  {
    _recipeRepository = recipeRepository;
    _waferRepository  = waferRepository;
    _equipmentConfig  = equipmentConfig;

    RecalculateFovFromEquipment();
  }

  // ── 커맨드 ─────────────────────────────────────────────────────────────

  /// <summary>장비 설정에서 FOV를 재계산하여 필드에 반영한다.</summary>
  [RelayCommand]
  private void RecalculateFovFromEquipment() => Execute(() =>
  {
    var cfg          = _equipmentConfig.Config;
    double effective = cfg.EffectivePixelSizeUm;
    FovWidthUm  = cfg.SensorWidth  * effective;
    FovHeightUm = cfg.SensorHeight * effective;
  }, nameof(RecalculateFovFromEquipment));

  /// <summary>WaferInfo DB 목록을 새로고침한다.</summary>
  [RelayCommand]
  private async Task RefreshWaferListAsync() => await Execute(async () =>
  {
    var list = await _waferRepository.ListAsync();
    WaferInfoList.Clear();
    foreach (var item in list)
      WaferInfoList.Add(item);
  }, nameof(RefreshWaferListAsync));

  /// <summary>선택된 WaferInfo를 Recipe에 연결한다.</summary>
  [RelayCommand(CanExecute = nameof(HasSelectedWaferInfo))]
  private void SelectWaferInfo() => Execute(() =>
  {
    SetWaferInfo(SelectedWaferInfo!);
  }, nameof(SelectWaferInfo));

  /// <summary>현재 편집 값으로 Recipe를 생성하고 Repository에 저장한다.</summary>
  [RelayCommand(CanExecute = nameof(HasWaferInfo))]
  private async Task SaveAsync() => await Execute(async () =>
  {
    var recipe = BuildRecipe();
    await _recipeRepository.SaveAsync(recipe);
    SavedRecipeName = recipe.RecipeName;
    await RefreshRecipeListAsync();
  }, nameof(SaveAsync));

  /// <summary>Recipe DB 목록을 새로고침한다.</summary>
  [RelayCommand]
  private async Task RefreshRecipeListAsync() => await Execute(async () =>
  {
    var list = await _recipeRepository.ListAsync();
    RecipeList.Clear();
    foreach (var item in list)
      RecipeList.Add(item);
  }, nameof(RefreshRecipeListAsync));

  /// <summary>선택된 Recipe를 폼에 채운다.</summary>
  [RelayCommand(CanExecute = nameof(HasSelectedRecipe))]
  private void LoadSelected() => Execute(() =>
  {
    var recipe = SelectedRecipe!;
    SetWaferInfo(recipe.Wafer);
    ApplyToForm(recipe);
    SavedRecipeName = recipe.RecipeName;
  }, nameof(LoadSelected));

  /// <summary>선택된 Recipe를 DB에서 삭제한다.</summary>
  [RelayCommand(CanExecute = nameof(HasSelectedRecipe))]
  private async Task DeleteSelectedAsync() => await Execute(async () =>
  {
    var toDelete = SelectedRecipe!;
    await _recipeRepository.DeleteAsync(toDelete.RecipeName);
    SelectedRecipe = null;
    await RefreshRecipeListAsync();
  }, nameof(DeleteSelectedAsync));

  // ── CanExecute 헬퍼 ───────────────────────────────────────────────────

  private bool HasSelectedRecipe    => SelectedRecipe is not null;
  private bool HasSelectedWaferInfo => SelectedWaferInfo is not null;

  // ── 내부 헬퍼 ─────────────────────────────────────────────────────────

  private void SetWaferInfo(WaferInfo info)
  {
    _loadedWaferInfo = info;
    HasWaferInfo     = true;
    WaferSummary     = $"{info.WaferId} | {info.WaferType} | Die {info.DieSize.WidthUm:0}×{info.DieSize.HeightUm:0} µm";
  }

  private WaferSurfaceInspectionRecipe BuildRecipe() => new(
    RecipeName:      RecipeName,
    Description:     Description,
    Wafer:           _loadedWaferInfo!,
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
    FovWidthUm      = recipe.Fov.WidthUm;
    FovHeightUm     = recipe.Fov.HeightUm;
    OverlapXum      = recipe.OverlapXum;
    OverlapYum      = recipe.OverlapYum;
    StopOnFirstFail = recipe.StopOnFirstFail;
    MaxFrameCount   = recipe.MaxFrameCount;
  }
}
