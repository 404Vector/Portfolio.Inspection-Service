using System.Collections.Specialized;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using InspectionClient.Models;

namespace InspectionClient.Controls;

public partial class LogControl : UserControl
{
    public static readonly StyledProperty<ObservableCollection<LogEntry>?> EntriesProperty =
        AvaloniaProperty.Register<LogControl, ObservableCollection<LogEntry>?>(nameof(Entries));

    public ObservableCollection<LogEntry>? Entries
    {
        get => GetValue(EntriesProperty);
        set => SetValue(EntriesProperty, value);
    }

    public LogControl()
    {
        InitializeComponent();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != EntriesProperty) return;

        if (change.OldValue is ObservableCollection<LogEntry> old)
            old.CollectionChanged -= OnEntriesChanged;

        if (change.NewValue is ObservableCollection<LogEntry> current)
            current.CollectionChanged += OnEntriesChanged;
    }

    private void OnClearClicked(object? sender, RoutedEventArgs e)
        => Entries?.Clear();

    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add) return;

        // 레이아웃 패스 완료 후 스크롤 — Background 우선순위로 큐잉
        Dispatcher.UIThread.Post(
            () => LogScrollViewer.Offset = new Vector(LogScrollViewer.Offset.X, double.MaxValue),
            DispatcherPriority.Background);
    }
}
