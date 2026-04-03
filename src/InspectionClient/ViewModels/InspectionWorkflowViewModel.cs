using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Core.Logging.Interfaces;
using Core.Models;
using InspectionClient.Interfaces;
using InspectionClient.Models;
using Core.Recipe.Models;

namespace InspectionClient.ViewModels;

/// <summary>
/// Inspection 워크플로 ViewModel.
///
/// 책임:
///   - IRecipeRepository에서 Recipe 로드
///   - IWaferInfoRepository에서 Wafer 로드
///   - IInspectionService를 통한 검사 실행
///   - 실시간 WaferMap 상태 업데이트
///   - 완료된 검사 결과를 IInspectionResultRepository에 저장
/// </summary>
public partial class InspectionWorkflowViewModel : ViewModelBase
{
  private readonly IRecipeRepository           _recipeRepository;
  private readonly IInspectionResultRepository _resultRepository;
  private readonly IInspectionService          _inspectionService;
  private readonly IWaferInfoRepository        _waferRepository;

  private DateTimeOffset _inspectionStartedAt;

  // ── 상태 표시 ──────────────────────────────────────────────────────────

  [ObservableProperty] private string _recipeSummary = "(Recipe를 선택하세요)";
  [ObservableProperty] private string _waferSummary  = "(Wafer를 선택하세요)";
  [ObservableProperty] private string _progressText  = string.Empty;
  [ObservableProperty] private double _progressRatio;

  [ObservableProperty]
  [NotifyCanExecuteChangedFor(nameof(StartCommand))]
  [NotifyCanExecuteChangedFor(nameof(StopCommand))]
  private bool _isRunning;

  // ── WaferMap 바인딩 ───────────────────────────────────────────────────

  [ObservableProperty] private DieMap? _dieMap;

  [ObservableProperty]
  private IReadOnlyDictionary<DieIndex, DieInspectionState>? _dieStatuses;

  private readonly Dictionary<DieIndex, DieInspectionState> _statusMap = new();

  // ── Recipe 목록 패널 ─────────────────────────────────────────────────

  public ObservableCollection<RecipeRow> RecipeList { get; } = new();

  [ObservableProperty]
  [NotifyCanExecuteChangedFor(nameof(LoadSelectedRecipeCommand))]
  private RecipeRow? _selectedRecipe;

  [ObservableProperty]
  [NotifyCanExecuteChangedFor(nameof(StartCommand))]
  private RecipeRow? _loadedRecipe;

  // ── Wafer 목록 패널 ──────────────────────────────────────────────────

  public ObservableCollection<WaferInfoRow> WaferList { get; } = new();

  [ObservableProperty]
  [NotifyCanExecuteChangedFor(nameof(LoadSelectedWaferCommand))]
  private WaferInfoRow? _selectedWafer;

  [ObservableProperty]
  [NotifyCanExecuteChangedFor(nameof(StartCommand))]
  private WaferInfoRow? _loadedWafer;

  public InspectionWorkflowViewModel(
      IRecipeRepository           recipeRepository,
      IInspectionResultRepository resultRepository,
      IInspectionService          inspectionService,
      IWaferInfoRepository        waferRepository,
      ILogService                 logService)
      : base(logService)
  {
    _recipeRepository  = recipeRepository;
    _resultRepository  = resultRepository;
    _inspectionService = inspectionService;
    _waferRepository   = waferRepository;

    _inspectionService.ProgressChanged += OnProgressChanged;
    _inspectionService.Completed       += OnCompleted;

    _ = RefreshRecipeListAsync();
    _ = RefreshWaferListAsync();
  }

  // ── 검사 이벤트 핸들러 ────────────────────────────────────────────────

  private void OnProgressChanged(object? sender, InspectionProgressEventArgs e)
  {
    foreach (var key in _statusMap.Keys)
    {
      if (_statusMap[key] == DieInspectionState.Current)
      {
        _statusMap[key] = e.Passed ? DieInspectionState.Pass : DieInspectionState.Fail;
        break;
      }
    }
    _statusMap[e.DieIndex] = DieInspectionState.Current;

    DieStatuses   = new Dictionary<DieIndex, DieInspectionState>(_statusMap);
    ProgressText  = $"{e.CompletedShots} / {e.TotalShots} shots";
    ProgressRatio = e.ProgressRatio;
  }

  private async void OnCompleted(object? sender, InspectionCompletedEventArgs e)
  {
    foreach (var key in new List<DieIndex>(_statusMap.Keys))
    {
      if (_statusMap[key] == DieInspectionState.Current)
        _statusMap[key] = DieInspectionState.Pass;
    }

    DieStatuses  = new Dictionary<DieIndex, DieInspectionState>(_statusMap);
    IsRunning    = false;
    ProgressText = e.Cancelled ? "중단됨" : "완료";

    await SaveResultAsync(e.Cancelled);
  }

  private async Task SaveResultAsync(bool cancelled)
  {
    if (LoadedRecipe?.Recipe is null || LoadedWafer is null) return;

    var status = cancelled
        ? Core.Enums.InspectionStatus.Aborted
        : Core.Enums.InspectionStatus.Pass;

    var result = new WaferSurfaceInspectionResult(
      RecipeName:   LoadedRecipe.Recipe.RecipeName,
      WaferId:      LoadedWafer.WaferId,
      Status:       status,
      StartedAt:    _inspectionStartedAt,
      CompletedAt:  DateTimeOffset.UtcNow,
      FrameResults: Array.Empty<FrameInspectionResult>()
    );

    try
    {
      await _resultRepository.SaveAsync(result);
    }
    catch (Exception ex)
    {
      _log.Error(this, $"검사 결과 저장 실패: {ex.Message}");
    }
  }

  // ── 커맨드 ─────────────────────────────────────────────────────────────

  private bool CanStart() => !IsRunning
      && LoadedRecipe?.Recipe is not null
      && LoadedWafer is not null;
  private bool CanStop()  => IsRunning;

  [RelayCommand(CanExecute = nameof(CanStart))]
  private async Task StartAsync() => await Execute(async () =>
  {
    var recipe = LoadedRecipe!.Recipe;
    var wafer  = LoadedWafer!.ToWaferInfo();
    ResetMap();
    IsRunning            = true;
    _inspectionStartedAt = DateTimeOffset.UtcNow;
    await _inspectionService.StartAsync(recipe, wafer);
  }, nameof(StartAsync));

  [RelayCommand(CanExecute = nameof(CanStop))]
  private async Task StopAsync() => await Execute(async () =>
  {
    await _inspectionService.StopAsync();
  }, nameof(StopAsync));

  /// <summary>Recipe DB 목록을 새로고침한다.</summary>
  [RelayCommand]
  private async Task RefreshRecipeListAsync() => await Execute(async () =>
  {
    var list = await _recipeRepository.ListAsync();
    RecipeList.Clear();
    foreach (var row in list)
      RecipeList.Add(row);
  }, nameof(RefreshRecipeListAsync));

  /// <summary>선택된 Recipe를 검사에 연결한다.</summary>
  [RelayCommand(CanExecute = nameof(HasSelectedRecipe))]
  private void LoadSelectedRecipe() => Execute(() =>
  {
    LoadedRecipe  = SelectedRecipe!;
    var r = LoadedRecipe.Recipe;
    RecipeSummary = $"{r.RecipeName} | FOV {r.Fov.WidthUm:0}×{r.Fov.HeightUm:0} µm";
  }, nameof(LoadSelectedRecipe));

  /// <summary>Recipe 선택을 해제한다.</summary>
  [RelayCommand]
  private void UnloadRecipe() => Execute(() =>
  {
    LoadedRecipe  = null;
    RecipeSummary = "(Recipe를 선택하세요)";
  }, nameof(UnloadRecipe));

  /// <summary>Wafer DB 목록을 새로고침한다.</summary>
  [RelayCommand]
  private async Task RefreshWaferListAsync() => await Execute(async () =>
  {
    var list = await _waferRepository.ListAsync();
    WaferList.Clear();
    foreach (var row in list)
      WaferList.Add(row);
  }, nameof(RefreshWaferListAsync));

  /// <summary>선택된 Wafer를 검사에 연결한다.</summary>
  [RelayCommand(CanExecute = nameof(HasSelectedWafer))]
  private void LoadSelectedWafer() => Execute(() =>
  {
    LoadedWafer  = SelectedWafer!;
    var wafer    = LoadedWafer.ToWaferInfo();
    WaferSummary = $"{wafer.WaferId} | {wafer.WaferType}";
    DieMap       = DieMap.From(wafer);
    ResetMap();
  }, nameof(LoadSelectedWafer));

  /// <summary>Wafer 선택을 해제한다.</summary>
  [RelayCommand]
  private void UnloadWafer() => Execute(() =>
  {
    LoadedWafer  = null;
    WaferSummary = "(Wafer를 선택하세요)";
    DieMap       = null;
    ResetMap();
  }, nameof(UnloadWafer));

  // ── CanExecute 헬퍼 ───────────────────────────────────────────────────

  private bool HasSelectedRecipe => SelectedRecipe is not null;
  private bool HasSelectedWafer  => SelectedWafer is not null;

  // ── 내부 헬퍼 ─────────────────────────────────────────────────────────

  private void ResetMap()
  {
    _statusMap.Clear();
    DieStatuses   = null;
    ProgressText  = string.Empty;
    ProgressRatio = 0.0;
  }
}
