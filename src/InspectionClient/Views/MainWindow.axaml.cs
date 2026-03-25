using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using InspectionClient.ViewModels;

namespace InspectionClient.Views;

public partial class MainWindow : Window
{
    // 버튼 Name → 논리적 뷰 이름 매핑 (PointerEntered hover 미리보기용)
    private static readonly Dictionary<string, string> ButtonToViewName = new()
    {
        [nameof(NavInspection)]   = "Inspection",
        [nameof(NavHistory)]      = "History",
        [nameof(NavOpticSetting)] = "OpticSetting",
        [nameof(NavAppSetting)]   = "AppSetting",
    };

    private MainWindowViewModel view_model => (MainWindowViewModel)DataContext!;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    // ActiveViewName이 바뀔 때 nav 버튼의 active CSS 클래스를 동기화
    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        UpdateActiveButton(vm.ActiveViewName);
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainWindowViewModel.ActiveViewName))
                UpdateActiveButton(vm.ActiveViewName);
        };
    }

    private void UpdateActiveButton(string activeViewName)
    {
        var buttons = new Dictionary<string, Button?>
        {
            ["Inspection"]   = NavInspection,
            ["History"]      = NavHistory,
            ["OpticSetting"] = NavOpticSetting,
            ["AppSetting"]   = NavAppSetting,
        };

        foreach (var (name, btn) in buttons)
        {
            if (btn is null) continue;
            if (name == activeViewName)
                btn.Classes.Add("active");
            else
                btn.Classes.Remove("active");
        }
    }

    private void OnNavPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is Button { Name: { } name }
            && ButtonToViewName.TryGetValue(name, out var viewName))
        {
            view_model.HoverEnterCommand.Execute(viewName);
        }
    }

    private void OnNavPointerExited(object? sender, PointerEventArgs e)
    {
        view_model.HoverExitCommand.Execute(null);
    }
}
