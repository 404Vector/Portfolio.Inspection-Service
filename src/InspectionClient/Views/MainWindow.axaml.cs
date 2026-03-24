using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace InspectionClient.Views;

public partial class MainWindow : Window
{
    private readonly InspectionView   _inspectionView   = new();
    private readonly HistoryView      _historyView      = new();
    private readonly OpticSettingView _opticSettingView = new();
    private readonly AppSettingView   _appSettingView   = new();

    private Button? _activeNavButton;
    private string  _currentViewName = string.Empty;

    private static readonly System.Collections.Generic.Dictionary<string, string> NavNames = new()
    {
        { nameof(NavInspection),   "Inspection"    },
        { nameof(NavHistory),      "History"       },
        { nameof(NavOpticSetting), "Optic Setting" },
        { nameof(NavAppSetting),   "App Setting"   },
    };

    public MainWindow()
    {
        InitializeComponent();
        Navigate(NavInspection, _inspectionView);
    }

    private void OnNavClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;

        var view = button.Name switch
        {
            nameof(NavInspection)   => (Control)_inspectionView,
            nameof(NavHistory)      => _historyView,
            nameof(NavOpticSetting) => _opticSettingView,
            nameof(NavAppSetting)   => _appSettingView,
            _                       => null
        };

        if (view is not null)
            Navigate(button, view);
    }

    private void OnNavPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is not Button button || button.Name is null) return;
        if (!NavNames.TryGetValue(button.Name, out var hoverName)) return;

        TitleText.Text = $"{_currentViewName} > {hoverName}";
    }

    private void OnNavPointerExited(object? sender, PointerEventArgs e)
    {
        TitleText.Text = _currentViewName;
    }

    private void Navigate(Button navButton, Control view)
    {
        _activeNavButton?.Classes.Remove("active");
        navButton.Classes.Add("active");
        _activeNavButton = navButton;

        if (navButton.Name is not null && NavNames.TryGetValue(navButton.Name, out var name))
        {
            _currentViewName = name;
            TitleText.Text   = name;
        }

        MainContent.Content = view;
    }
}
