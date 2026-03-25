using System;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using InspectionClient.ViewModels;

namespace InspectionClient.Views;

public partial class LogControl : UserControl
{
    private LogViewModel? _vm;

    public LogControl()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_vm is not null)
            _vm.Entries.CollectionChanged -= OnEntriesChanged;

        _vm = DataContext as LogViewModel;

        if (_vm is not null)
            _vm.Entries.CollectionChanged += OnEntriesChanged;
    }

    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add) return;

        // 레이아웃 패스 완료 후 스크롤 — Background 우선순위로 큐잉
        Dispatcher.UIThread.Post(
            () => LogScrollViewer.Offset = new Vector(LogScrollViewer.Offset.X, double.MaxValue),
            DispatcherPriority.Background);
    }
}
