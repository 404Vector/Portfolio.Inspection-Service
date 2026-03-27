using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InspectionClient.Interfaces;

namespace InspectionClient.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private static readonly Dictionary<string, string> DisplayNames = new()
    {
        ["Inspection"]    = "Inspection",
        ["History"]       = "History",
        ["OpticSetting"]  = "Optic Setting",
        ["EquipmentSpec"] = "Equipment Spec",
        ["AppSetting"]    = "App Setting",
    };

    [ObservableProperty] private ViewModelBase _currentViewModel = null!;
    [ObservableProperty] private string _activeViewName = string.Empty;
    [ObservableProperty] private string _titleText = string.Empty;

    public IObservableLogService Log { get; }

    private readonly InspectionViewModel    _inspectionVm;
    private readonly HistoryViewModel       _historyVm;
    private readonly OpticSettingViewModel  _opticSettingVm;
    private readonly EquipmentSpecViewModel _equipmentSpecVm;
    private readonly AppSettingViewModel    _appSettingVm;

    public MainWindowViewModel(
        InspectionViewModel    inspectionVm,
        HistoryViewModel       historyVm,
        OpticSettingViewModel  opticSettingVm,
        EquipmentSpecViewModel equipmentSpecVm,
        AppSettingViewModel    appSettingVm,
        IObservableLogService  logService) : base(logService)
    {
        _inspectionVm    = inspectionVm;
        _historyVm       = historyVm;
        _opticSettingVm  = opticSettingVm;
        _equipmentSpecVm = equipmentSpecVm;
        _appSettingVm    = appSettingVm;
        Log              = logService;

        Navigate("Inspection");
    }

    [RelayCommand]
    private void Navigate(string viewName) => Execute(() =>
    {
        CurrentViewModel = viewName switch
        {
            "Inspection"    => _inspectionVm,
            "History"       => _historyVm,
            "OpticSetting"  => _opticSettingVm,
            "EquipmentSpec" => _equipmentSpecVm,
            "AppSetting"    => _appSettingVm,
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
