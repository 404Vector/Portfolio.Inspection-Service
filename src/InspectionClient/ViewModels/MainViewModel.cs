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
    [ServiceKeys.Inspection]    = ServiceKeys.Inspection,
    ["History"]        = "History",
    ["FrameGrabber"]   = "Frame Grabber",
    ["EquipmentSpec"]  = "Equipment Spec",
    ["AppSetting"]     = "App Setting",
    ["DieRendering"]   = "Die Rendering",
  };

  [ObservableProperty] private ViewModelBase _currentViewModel = null!;
  [ObservableProperty] private string _activeViewName = string.Empty;
  [ObservableProperty] private string _titleText = string.Empty;
  [ObservableProperty] private bool _isFrameGrabberConnected;
  [ObservableProperty] private bool _isInspectionConnected;

  public IObservableLogService Log { get; }

  private readonly InspectionViewModel      _inspectionVm;
  private readonly HistoryViewModel         _historyVm;
  private readonly FrameGrabberViewModelBase _fgVm;
  private readonly EquipmentSpecViewModel   _equipmentSpecVm;
  private readonly AppSettingViewModel      _appSettingVm;
  private readonly DieRenderingViewModel    _dieRenderingVm;

  public MainViewModel(
      InspectionViewModel         inspectionVm,
      HistoryViewModel            historyVm,
      FrameGrabberViewModelBase   fgVm,
      EquipmentSpecViewModel      equipmentSpecVm,
      AppSettingViewModel         appSettingVm,
      DieRenderingViewModel       dieRenderingVm,
      IObservableLogService       logService,
      IServiceConnectionMonitor   connectionMonitor) : base(logService)
  {
    _inspectionVm    = inspectionVm;
    _historyVm       = historyVm;
    _fgVm            = fgVm;
    _equipmentSpecVm = equipmentSpecVm;
    _appSettingVm    = appSettingVm;
    _dieRenderingVm  = dieRenderingVm;
    Log              = logService;

    // 초기 상태 반영
    IsFrameGrabberConnected = connectionMonitor.States.GetValueOrDefault(GrpcFrameGrabberProbe.Key);
    IsInspectionConnected   = connectionMonitor.States.GetValueOrDefault(ServiceKeys.Inspection);

    connectionMonitor.StateChanged += (_, e) =>
    {
      if (e.ServiceKey == GrpcFrameGrabberProbe.Key)
        IsFrameGrabberConnected = e.IsConnected;
      else if (e.ServiceKey == ServiceKeys.Inspection)
        IsInspectionConnected = e.IsConnected;
    };

    Navigate(ServiceKeys.Inspection);
  }

  [RelayCommand]
  private void Navigate(string viewName) => Execute(() =>
  {
    CurrentViewModel = viewName switch
    {
      ServiceKeys.Inspection    => _inspectionVm,
      "History"       => _historyVm,
      "FrameGrabber"  => _fgVm,
      "EquipmentSpec" => _equipmentSpecVm,
      "AppSetting"    => _appSettingVm,
      "DieRendering"  => _dieRenderingVm,
      _               => CurrentViewModel
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
