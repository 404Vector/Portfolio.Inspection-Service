using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InspectionClient.Interfaces;
using InspectionClient.Services.Probes;

namespace InspectionClient.ViewModels;

public partial class MainViewModel : ViewModelBase
{
  private static readonly Dictionary<string, string> DisplayNames = new()
  {
    [ServiceKeys.Inspection]  = ServiceKeys.Inspection,
    ["DieSetup"]              = "Die Setup",
    ["WaferSetup"]            = "Wafer Setup",
    ["RecipeSetup"]           = "Recipe Setup",
    ["History"]               = "History",
    ["FrameGrabber"]          = "Frame Grabber",
    ["EquipmentSpec"]         = "Equipment Spec",
    ["AppSetting"]            = "App Setting",
  };

  [ObservableProperty] private ViewModelBase _currentViewModel = null!;
  [ObservableProperty] private string _activeViewName = string.Empty;
  [ObservableProperty] private string _titleText      = string.Empty;
  [ObservableProperty] private bool   _isFrameGrabberConnected;
  [ObservableProperty] private bool   _isInspectionConnected;

  public IObservableLogService Log { get; }

  private readonly InspectionWorkflowViewModel   _inspectionVm;
  private readonly DieSetupWorkflowViewModel     _dieSetupVm;
  private readonly WaferSetupWorkflowViewModel   _waferSetupVm;
  private readonly RecipeSetupWorkflowViewModel  _recipeSetupVm;
  private readonly HistoryViewModel              _historyVm;
  private readonly FrameGrabberViewModelBase     _fgVm;
  private readonly EquipmentSpecViewModel        _equipmentSpecVm;
  private readonly AppSettingViewModel           _appSettingVm;

  public MainViewModel(
      InspectionWorkflowViewModel   inspectionVm,
      DieSetupWorkflowViewModel     dieSetupVm,
      WaferSetupWorkflowViewModel   waferSetupVm,
      RecipeSetupWorkflowViewModel  recipeSetupVm,
      HistoryViewModel              historyVm,
      FrameGrabberViewModelBase     fgVm,
      EquipmentSpecViewModel        equipmentSpecVm,
      AppSettingViewModel           appSettingVm,
      IObservableLogService         logService,
      IServiceConnectionMonitor     connectionMonitor) : base(logService)
  {
    _inspectionVm    = inspectionVm;
    _dieSetupVm      = dieSetupVm;
    _waferSetupVm    = waferSetupVm;
    _recipeSetupVm   = recipeSetupVm;
    _historyVm       = historyVm;
    _fgVm            = fgVm;
    _equipmentSpecVm = equipmentSpecVm;
    _appSettingVm    = appSettingVm;
    Log              = logService;

    IsFrameGrabberConnected = connectionMonitor.States.GetValueOrDefault(GrpcFrameGrabberProbe.Key);
    IsInspectionConnected   = connectionMonitor.States.GetValueOrDefault(ServiceKeys.Inspection);

    connectionMonitor.StateChanged += (_, e) =>
    {
      if (e.ServiceKey == GrpcFrameGrabberProbe.Key)
        IsFrameGrabberConnected = e.IsConnected;
      else if (e.ServiceKey == ServiceKeys.Inspection)
        IsInspectionConnected = e.IsConnected;
    };

    Navigate("DieSetup");
  }

  // ── 커맨드 ───────────────────────────────────────────────────────────

  [RelayCommand]
  private void Navigate(string viewName) => Execute(() =>
  {
    CurrentViewModel = viewName switch
    {
      ServiceKeys.Inspection => _inspectionVm,
      "DieSetup"             => _dieSetupVm,
      "WaferSetup"           => _waferSetupVm,
      "RecipeSetup"          => _recipeSetupVm,
      "History"              => _historyVm,
      "FrameGrabber"         => _fgVm,
      "EquipmentSpec"        => _equipmentSpecVm,
      "AppSetting"           => _appSettingVm,
      _                      => CurrentViewModel
    };
    ActiveViewName = viewName;
    TitleText = DisplayNames.GetValueOrDefault(viewName, viewName);
  }, nameof(Navigate));

  [RelayCommand]
  private void HoverEnter(string viewName)
  {
    var current = DisplayNames.GetValueOrDefault(ActiveViewName, ActiveViewName);
    var hover   = DisplayNames.GetValueOrDefault(viewName, viewName);
    TitleText = $"{current} > {hover}";
  }

  [RelayCommand]
  private void HoverExit()
  {
    TitleText = DisplayNames.GetValueOrDefault(ActiveViewName, ActiveViewName);
  }
}
