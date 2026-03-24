using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace InspectionClient.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private static readonly Dictionary<string, string> DisplayNames = new()
    {
        ["Inspection"]   = "Inspection",
        ["History"]      = "History",
        ["OpticSetting"] = "Optic Setting",
        ["AppSetting"]   = "App Setting",
    };

    [ObservableProperty] private ViewModelBase _currentViewModel = null!;
    [ObservableProperty] private string _activeViewName = string.Empty;
    [ObservableProperty] private string _titleText = string.Empty;

    private readonly InspectionViewModel   _inspectionVm;
    private readonly HistoryViewModel      _historyVm;
    private readonly OpticSettingViewModel _opticSettingVm;
    private readonly AppSettingViewModel   _appSettingVm;

    public MainWindowViewModel(
        InspectionViewModel   inspectionVm,
        HistoryViewModel      historyVm,
        OpticSettingViewModel opticSettingVm,
        AppSettingViewModel   appSettingVm)
    {
        _inspectionVm   = inspectionVm;
        _historyVm      = historyVm;
        _opticSettingVm = opticSettingVm;
        _appSettingVm   = appSettingVm;

        Navigate("Inspection");
    }

    [RelayCommand]
    private void Navigate(string viewName)
    {
        CurrentViewModel = viewName switch
        {
            "Inspection"   => _inspectionVm,
            "History"      => _historyVm,
            "OpticSetting" => _opticSettingVm,
            "AppSetting"   => _appSettingVm,
            _              => CurrentViewModel
        };
        ActiveViewName = viewName;
        TitleText = DisplayNames.GetValueOrDefault(viewName, viewName);
    }

    // View가 hover 진입 시 호출 — 제목 미리보기 표시
    public void OnHoverEnter(string viewName)
    {
        var current = DisplayNames.GetValueOrDefault(ActiveViewName, ActiveViewName);
        var hover   = DisplayNames.GetValueOrDefault(viewName, viewName);
        TitleText = $"{current} > {hover}";
    }

    // View가 hover 종료 시 호출 — 현재 뷰 제목 복원
    public void OnHoverExit()
    {
        TitleText = DisplayNames.GetValueOrDefault(ActiveViewName, ActiveViewName);
    }
}
