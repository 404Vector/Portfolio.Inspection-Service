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
using InspectionRecipe.Models;

namespace InspectionClient.ViewModels;

/// <summary>
/// Inspection 워크플로 ViewModel.
///
/// 책임:
///   - IRecipeRepository에서 Recipe 로드
///   - IInspectionService를 통한 검사 실행
///   - 실시간 WaferMap 상태 업데이트
///   - 완료된 검사 결과를 IInspectionResultRepository에 저장
///   - 검사 결과 목록 표시
/// </summary>
public partial class InspectionWorkflowViewModel : ViewModelBase
{
  private readonly IRecipeRepository           _recipeRepository;
  private readonly IInspectionResultRepository _resultRepository;
  private readonly IInspectionService          _inspectionService;

  private WaferSurfaceInspectionRecipe? _loadedRecipe;
  private DateTimeOffset                _inspectionStartedAt;

  // ── 상태 표시 ──────────────────────────────────────────────────────────

  [ObservableProperty] private string _recipeSummary = "(없음 — Recipe 목록에서 선택하세요)";
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

  public ObservableCollection<WaferSurfaceInspectionRecipe> RecipeList { get; } = new();

  [ObservableProperty]
  [NotifyCanExecuteChangedFor(nameof(LoadSelectedRecipeCommand))]
  private WaferSurfaceInspectionRecipe? _selectedRecipe;

  // ── 검사 결과 목록 패널 ──────────────────────────────────────────────

  public ObservableCollection<InspectionResultEntry> ResultList { get; } = new();

  [ObservableProperty]
  [NotifyCanExecuteChangedFor(nameof(DeleteSelectedResultCommand))]
  private InspectionResultEntry? _selectedResult;

  public InspectionWorkflowViewModel(
      IRecipeRepository           recipeRepository,
      IInspectionResultRepository resultRepository,
      IInspectionService          inspectionService,
      ILogService                 logService)
      : base(logService)
  {
    _recipeRepository  = recipeRepository;
    _resultRepository  = resultRepository;
    _inspectionService = inspectionService;

    _inspectionService.ProgressChanged += OnProgressChanged;
    _inspectionService.Completed       += OnCompleted;
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
    await RefreshResultListAsync();
  }

  private async Task SaveResultAsync(bool cancelled)
  {
    if (_loadedRecipe is null) return;

    var status = cancelled
        ? Core.Enums.InspectionStatus.Aborted
        : Core.Enums.InspectionStatus.Pass;

    var result = new WaferSurfaceInspectionResult(
      RecipeName:   _loadedRecipe.RecipeName,
      Wafer:        _loadedRecipe.Wafer,
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

  private bool CanStart() => !IsRunning && _loadedRecipe is not null;
  private bool CanStop()  => IsRunning;

  [RelayCommand(CanExecute = nameof(CanStart))]
  private async Task StartAsync() => await Execute(async () =>
  {
    var recipe = _loadedRecipe!;
    ResetMap();
    IsRunning            = true;
    _inspectionStartedAt = DateTimeOffset.UtcNow;
    await _inspectionService.StartAsync(recipe);
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
    foreach (var item in list)
      RecipeList.Add(item);
  }, nameof(RefreshRecipeListAsync));

  /// <summary>선택된 Recipe를 검사에 연결한다.</summary>
  [RelayCommand(CanExecute = nameof(HasSelectedRecipe))]
  private void LoadSelectedRecipe() => Execute(() =>
  {
    SetRecipe(SelectedRecipe!);
  }, nameof(LoadSelectedRecipe));

  /// <summary>검사 결과 DB 목록을 새로고침한다.</summary>
  [RelayCommand]
  private async Task RefreshResultListAsync() => await Execute(async () =>
  {
    var list = await _resultRepository.ListEntriesAsync();
    ResultList.Clear();
    foreach (var item in list)
      ResultList.Add(item);
  }, nameof(RefreshResultListAsync));

  /// <summary>선택된 검사 결과를 DB에서 삭제한다.</summary>
  [RelayCommand(CanExecute = nameof(HasSelectedResult))]
  private async Task DeleteSelectedResultAsync() => await Execute(async () =>
  {
    var toDelete = SelectedResult!;
    await _resultRepository.DeleteAsync(toDelete.ResultId);
    SelectedResult = null;
    await RefreshResultListAsync();
  }, nameof(DeleteSelectedResultAsync));

  // ── CanExecute 헬퍼 ───────────────────────────────────────────────────

  private bool HasSelectedRecipe => SelectedRecipe is not null;
  private bool HasSelectedResult => SelectedResult is not null;

  // ── 내부 헬퍼 ─────────────────────────────────────────────────────────

  private void SetRecipe(WaferSurfaceInspectionRecipe recipe)
  {
    _loadedRecipe  = recipe;
    RecipeSummary  = $"{recipe.RecipeName} | {recipe.Wafer.WaferId} | FOV {recipe.Fov.WidthUm:0}×{recipe.Fov.HeightUm:0} µm";
    DieMap         = DieMap.From(recipe.Wafer);
    StartCommand.NotifyCanExecuteChanged();
    ResetMap();
  }

  private void ResetMap()
  {
    _statusMap.Clear();
    DieStatuses   = null;
    ProgressText  = string.Empty;
    ProgressRatio = 0.0;
  }
}
