using Core.Logging.Interfaces;

namespace InspectionClient.ViewModels;

/// <summary>
/// Setup 섹션의 Shell ViewModel (L2).
///
/// 책임: Die / Wafer / Recipe Setup ViewModel을 보유하여
/// SetupWorkflowView의 TabControl에 바인딩한다.
/// </summary>
public partial class SetupWorkflowViewModel : ViewModelBase
{
  public DieSetupWorkflowViewModel     DieSetup   { get; }
  public WaferSetupWorkflowViewModel   WaferSetup { get; }
  public RecipeSetupWorkflowViewModel  RecipeSetup { get; }

  public SetupWorkflowViewModel(
      DieSetupWorkflowViewModel    dieSetup,
      WaferSetupWorkflowViewModel  waferSetup,
      RecipeSetupWorkflowViewModel recipeSetup,
      ILogService                  logService) : base(logService)
  {
    DieSetup    = dieSetup;
    WaferSetup  = waferSetup;
    RecipeSetup = recipeSetup;
  }
}
