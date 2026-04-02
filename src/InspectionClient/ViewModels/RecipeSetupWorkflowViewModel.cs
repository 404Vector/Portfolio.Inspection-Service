using Core.Logging.Interfaces;

namespace InspectionClient.ViewModels;

/// <summary>
/// Recipe Setup 섹션의 Shell ViewModel (L3).
///
/// 책임: Recipe Type ViewModel을 보유하여 RecipeSetupWorkflowView의 탭에 바인딩한다.
/// 새 Recipe 타입 추가 시: 생성자 파라미터 추가, 프로퍼티 추가, View에 TabItem 추가.
/// </summary>
public partial class RecipeSetupWorkflowViewModel : ViewModelBase
{
  public WaferSurfaceRecipeWorkflowViewModel WaferSurface { get; }
  public DieSpotRecipeWorkflowViewModel      DieSpot      { get; }

  public RecipeSetupWorkflowViewModel(
      WaferSurfaceRecipeWorkflowViewModel waferSurface,
      DieSpotRecipeWorkflowViewModel      dieSpot,
      ILogService                         logService) : base(logService)
  {
    WaferSurface = waferSurface;
    DieSpot      = dieSpot;
  }
}
