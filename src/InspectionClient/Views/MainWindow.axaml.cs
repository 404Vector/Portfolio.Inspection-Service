using Avalonia.Controls;
using Avalonia.Interactivity;

namespace InspectionClient.Views;

public partial class MainWindow : Window
{
    private readonly InspectionView   _inspectionView   = new();
    private readonly HistoryView      _historyView      = new();
    private readonly OpticSettingView _opticSettingView = new();
    private readonly AppSettingView   _appSettingView   = new();

    private Button? _activeNavButton;

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

    private void Navigate(Button navButton, Control view)
    {
        _activeNavButton?.Classes.Remove("active");
        navButton.Classes.Add("active");
        _activeNavButton = navButton;

        MainContent.Content = view;
    }
}
