using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Core.Logging.Interfaces;
using Core.Models;
using InspectionClient.Interfaces;
using InspectionClient.Models;
using Core.Recipe.Models;

namespace InspectionClient.ViewModels;

/// <summary>
/// Wafer Surface Inspection Recipe CRUD 워크플로 ViewModel.
///
/// 책임:
///   - Recipe CRUD (목록 로드, 생성, 저장, 삭제)
///   - Recipe 파라미터 편집 (편집 버퍼)
///   - Load 시 편집 버퍼를 채움
///   - Save 시 편집 버퍼에서 새 RecipeRow를 조립하여 저장
///
/// Recipe는 WaferInfo 전체를 보유하지 않고 WaferId로만 참조한다.
/// WaferInfo의 물리 데이터가 필요한 시점(검사 실행, KLARF 생성)에
/// 호출자가 Repository에서 직접 조회하여 사용한다.
/// WaferId는 검사 실행 화면(InspectionWorkflowView)에서 Wafer를 선택할 때 결정한다.
/// </summary>
public partial class WaferSurfaceRecipeWorkflowViewModel : DbTableWorkflowViewModelBase<RecipeRow>
{
  private readonly IEquipmentConfigService _equipmentConfig;

  // ── Recipe 파라미터 편집 버퍼 ─────────────────────────────────────────

  [ObservableProperty] private string _recipeName      = "NewRecipe";
  [ObservableProperty] private string _description     = string.Empty;

  // FOV (읽기 전용 표시, magnification 선택으로 자동 계산)
  [ObservableProperty] private double _fovWidthUm         = 0.0;
  [ObservableProperty] private double _fovHeightUm        = 0.0;
  [ObservableProperty] private uint   _selectedMagnification = 2;
  [ObservableProperty] private double _pixelResolutionUm  = 0.0;

  // Overlap: 픽셀 단위 편집 → μm 자동 계산
  [ObservableProperty] private int    _overlapXPx = 0;
  [ObservableProperty] private int    _overlapYPx = 0;
  [ObservableProperty] private double _overlapXum = 0.0;
  [ObservableProperty] private double _overlapYum = 0.0;

  [ObservableProperty] private bool   _stopOnFirstFail = false;
  [ObservableProperty] private int    _maxFrameCount   = 0;

  // ── 보조편집패널 상태 ─────────────────────────────────────────────────

  public enum SidePanelMode { None, Magnification }

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(IsMagnificationPanelVisible))]
  private SidePanelMode _activePanel = SidePanelMode.None;

  public bool IsMagnificationPanelVisible => ActivePanel == SidePanelMode.Magnification;

  // ── 장비 설정 ─────────────────────────────────────────────────────────

  public IReadOnlyList<uint> AvailableMagnifications { get; }

  public WaferSurfaceRecipeWorkflowViewModel(
      IRecipeRepository       recipeRepository,
      IEquipmentConfigService equipmentConfig,
      ILogService             logService)
      : base(recipeRepository, logService)
  {
    _equipmentConfig        = equipmentConfig;
    AvailableMagnifications = equipmentConfig.Config.Magnifications.AsReadOnly();
    _ = RefreshAsync();
    RecalculateFov();
  }

  // ── 파생 훅 ──────────────────────────────────────────────────────────

  protected override void OnLoaded(RecipeRow row) => ApplyToForm(row.Recipe);

  protected override void OnBeforeSave(RecipeRow row) => row.Recipe = BuildRecipe();

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

  partial void OnOverlapXPxChanged(int value) => OverlapXum = value * PixelResolutionUm;

  partial void OnOverlapYPxChanged(int value) => OverlapYum = value * PixelResolutionUm;

  // ── 내부 헬퍼 ─────────────────────────────────────────────────────────

  private void RecalculateFov()
  {
    if (SelectedMagnification == 0)
      return;
    var cfg          = _equipmentConfig.Config;
    PixelResolutionUm = cfg.PixelPitchUm / SelectedMagnification;
    FovWidthUm        = cfg.SensorWidth  * PixelResolutionUm;
    FovHeightUm       = cfg.SensorHeight * PixelResolutionUm;
    OverlapXum        = OverlapXPx * PixelResolutionUm;
    OverlapYum        = OverlapYPx * PixelResolutionUm;
  }

  private WaferSurfaceInspectionRecipe BuildRecipe() => new(
    RecipeName:      RecipeName,
    Description:     Description,
    Fov:             new FovSize(FovWidthUm, FovHeightUm),
    Magnification:   SelectedMagnification,
    OverlapXum:      OverlapXum,
    OverlapYum:      OverlapYum,
    StopOnFirstFail: StopOnFirstFail,
    MaxFrameCount:   MaxFrameCount
  );

  private void ApplyToForm(WaferSurfaceInspectionRecipe recipe)
  {
    this._log.Trace(this, recipe.RecipeName);
    RecipeName            = recipe.RecipeName;
    Description           = recipe.Description;
    SelectedMagnification = recipe.Magnification;
    OverlapXPx            = PixelResolutionUm > 0
        ? (int)Math.Round(recipe.OverlapXum / PixelResolutionUm)
        : 0;
    OverlapYPx            = PixelResolutionUm > 0
        ? (int)Math.Round(recipe.OverlapYum / PixelResolutionUm)
        : 0;
    StopOnFirstFail       = recipe.StopOnFirstFail;
    MaxFrameCount         = recipe.MaxFrameCount;
  }
}
