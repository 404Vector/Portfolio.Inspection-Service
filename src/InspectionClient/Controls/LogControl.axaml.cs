using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Core.Logging.Enums;
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

  // 필터 적용된 뷰 — AXAML에서 바인딩
  public ObservableCollection<LogEntry> FilteredEntries { get; } = [];

  private readonly HashSet<LogLevel> _activeFilters =
      [LogLevel.Trace, LogLevel.Debug, LogLevel.Info, LogLevel.Warning, LogLevel.Error];

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

    RebuildFilteredEntries();
  }

  private void OnClearClicked(object? sender, RoutedEventArgs e)
    => Entries?.Clear();

  private void OnFilterClicked(object? sender, RoutedEventArgs e)
  {
    if (sender is not ToggleButton btn) return;
    if (!Enum.TryParse<LogLevel>(btn.Tag as string, out var level)) return;

    if (btn.IsChecked == true)
      _activeFilters.Add(level);
    else
      _activeFilters.Remove(level);

    RebuildFilteredEntries();
  }

  private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
  {
    if (e.Action == NotifyCollectionChangedAction.Reset)
    {
      FilteredEntries.Clear();
      return;
    }

    if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems is not null)
    {
      foreach (LogEntry entry in e.NewItems)
      {
        if (_activeFilters.Contains(entry.Level))
          FilteredEntries.Add(entry);
      }
    }

    ScrollToBottom();
  }

  private void RebuildFilteredEntries()
  {
    FilteredEntries.Clear();

    if (Entries is null) return;

    foreach (var entry in Entries.Where(e => _activeFilters.Contains(e.Level)))
      FilteredEntries.Add(entry);

    ScrollToBottom();
  }

  private void ScrollToBottom()
  {
    Dispatcher.UIThread.Post(
        () => LogScrollViewer.Offset = new Vector(LogScrollViewer.Offset.X, double.MaxValue),
        DispatcherPriority.Background);
  }
}
