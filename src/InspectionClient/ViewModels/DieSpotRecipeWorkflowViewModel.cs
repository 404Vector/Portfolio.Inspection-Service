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
/// Die Spot Inspection Recipe CRUD 워크플로 ViewModel.
///
/// 책임:
///   - Recipe CRUD (목록 로드, 생성, 저장, 삭제)
///   - Recipe 파라미터 편집 (편집 버퍼)
///   - Load 시 편집 버퍼를 채움
///   - Save 시 편집 버퍼에서 새 DieSpotRecipeRow를 조립하여 저장
///
/// WaferId는 검사 실행 화면에서 Wafer를 선택할 때 결정한다.
/// </summary>
public partial class DieSpotRecipeWorkflowViewModel : ViewModelBase
{
  private readonly IDieSpotRecipeRepository _repository;
  private readonly IEquipmentConfigService  _equipmentConfig;

  // ── Recipe 목록 ───────────────────────────────────────────────────────

  public ObservableCollection<DieSpotRecipeRow> Items { get; } = new();

  [ObservableProperty]
  [NotifyCanExecuteChangedFor(nameof(LoadCommand))]
  [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
  private DieSpotRecipeRow? _selectedItem;

  /// <summary>
  /// 현재 편집 중인 항목. DbTableControl.LoadedItem과 양방향 바인딩.
  /// null이면 Browse 상태, non-null이면 Edit 상태.
  /// </summary>
  [ObservableProperty] private DieSpotRecipeRow? _loadedItem;

  // ── Recipe 파라미터 편집 버퍼 ─────────────────────────────────────────

  [ObservableProperty] private string _recipeName  = "NewRecipe";
  [ObservableProperty] private string _description = string.Empty;

  // FOV (읽기 전용 표시, magnification 선택으로 자동 계산)
  [ObservableProperty] private double _fovWidthUm            = 0.0;
  [ObservableProperty] private double _fovHeightUm           = 0.0;
  [ObservableProperty] private uint   _selectedMagnification = 2;
  [ObservableProperty] private double _pixelResolutionUm     = 0.0;

  // Shot Center (웨이퍼 좌표계, µm)
  [ObservableProperty] private double _shotCenterXum = 0.0;
  [ObservableProperty] private double _shotCenterYum = 0.0;

  // 검사 파라미터
  [ObservableProperty] private double _threshold  = 0.5;
  [ObservableProperty] private bool   _saveOnFail = false;

  // ── 보조편집패널 상태 ─────────────────────────────────────────────────

  public enum SidePanelMode { None, Magnification }

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(IsMagnificationPanelVisible))]
  private SidePanelMode _activePanel = SidePanelMode.None;

  public bool IsMagnificationPanelVisible => ActivePanel == SidePanelMode.Magnification;

  // ── 장비 설정 ─────────────────────────────────────────────────────────

  public IReadOnlyList<uint> AvailableMagnifications { get; }

  public DieSpotRecipeWorkflowViewModel(
      IDieSpotRecipeRepository  repository,
      IEquipmentConfigService   equipmentConfig,
      ILogService               logService)
      : base(logService)
  {
    _repository             = repository;
    _equipmentConfig        = equipmentConfig;
    AvailableMagnifications = equipmentConfig.Config.Magnifications.AsReadOnly();
    _ = RefreshAsync();
    RecalculateFov();
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
    if (SelectedItem is not DieSpotRecipeRow row)
      return;
    ApplyToForm(row.Recipe);
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
    if (SelectedItem is not DieSpotRecipeRow current)
      return;
    await _repository.DeleteAsync(current.Id);
    Items.Remove(current);
    SelectedItem = null;
  }, nameof(DeleteAsync));

  [RelayCommand]
  private async Task SaveAsync() => await Execute(async () =>
  {
    if (LoadedItem is not DieSpotRecipeRow current)
      return;
    current.Recipe = BuildRecipe();
    await _repository.UpdateAsync(current);
    LoadedItem = null;
  }, nameof(SaveAsync));

  [RelayCommand]
  private async Task CancelAsync() => await Execute(async () =>
  {
    if (LoadedItem is not DieSpotRecipeRow current)
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
  private void OpenMagnificationPanel() =>
      ActivePanel = ActivePanel == SidePanelMode.Magnification
          ? SidePanelMode.None
          : SidePanelMode.Magnification;

  [RelayCommand]
  private void SelectMagnification(uint value)
  {
    SelectedMagnification = value;
    ActivePanel = SidePanelMode.None;
  }

  // ── 프로퍼티 변경 연동 ───────────────────────────────────────────────

  partial void OnSelectedMagnificationChanged(uint value) => RecalculateFov();

  // ── CanExecute 헬퍼 ─────────────────────────────────────────────────

  private bool HasSelectedItem => SelectedItem is not null;

  // ── 내부 헬퍼 ─────────────────────────────────────────────────────────

  private void RecalculateFov()
  {
    if (SelectedMagnification == 0)
      return;
    var cfg           = _equipmentConfig.Config;
    PixelResolutionUm = cfg.PixelPitchUm / SelectedMagnification;
    FovWidthUm        = cfg.SensorWidth  * PixelResolutionUm;
    FovHeightUm       = cfg.SensorHeight * PixelResolutionUm;
  }

  private DieSpotInspectionRecipe BuildRecipe() => new(
    RecipeName:  RecipeName,
    Description: Description,
    WaferId:     LoadedItem?.Recipe.WaferId ?? string.Empty,
    Fov:         new FovSize(FovWidthUm, FovHeightUm),
    ShotCenter:  new WaferCoordinate(ShotCenterXum, ShotCenterYum),
    Threshold:   Threshold,
    SaveOnFail:  SaveOnFail
  );

  private void ApplyToForm(DieSpotInspectionRecipe recipe)
  {
    RecipeName            = recipe.RecipeName;
    Description           = recipe.Description;
    SelectedMagnification = recipe.Fov.WidthUm > 0
        ? (uint)Math.Round(_equipmentConfig.Config.PixelPitchUm
            / (_equipmentConfig.Config.SensorWidth > 0
                ? recipe.Fov.WidthUm / _equipmentConfig.Config.SensorWidth
                : 1.0))
        : SelectedMagnification;
    ShotCenterXum = recipe.ShotCenter.Xum;
    ShotCenterYum = recipe.ShotCenter.Yum;
    Threshold     = recipe.Threshold;
    SaveOnFail    = recipe.SaveOnFail;
  }

  private DieSpotRecipeRow? FindById(long id)
  {
    foreach (var item in Items)
      if (item.Id == id)
        return item;
    return null;
  }
}
