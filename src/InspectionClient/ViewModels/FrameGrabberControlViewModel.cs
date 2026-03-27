using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Core.Logging.Interfaces;
using InspectionClient.Interfaces;
using InspectionClient.Models;

namespace InspectionClient.ViewModels;

public partial class FrameGrabberControlViewModel : ViewModelBase
{
  private readonly IFrameGrabberController _controller;

  public ObservableCollection<GrabberParameterItem> Parameters { get; } = new();
  public ObservableCollection<GrabberCommandItem>   Commands   { get; } = new();

  [ObservableProperty]
  private bool _isLoading;

  [ObservableProperty]
  private string _statusMessage = string.Empty;

  /// <summary>true이면 Triggered 모드 — Trigger 버튼을 활성화한다.</summary>
  [ObservableProperty]
  private bool _isTriggerMode;

  public FrameGrabberControlViewModel(ILogService logService, IFrameGrabberController controller)
      : base(logService)
  {
    _controller = controller;
    Parameters.CollectionChanged += OnParametersCollectionChanged;
  }

  // ── 초기화 ───────────────────────────────────────────────

  /// <summary>
  /// 서버로부터 Capabilities를 로드한다.
  /// 서버가 아직 준비되지 않은 경우 성공할 때까지 재시도한다.
  /// </summary>
  public async Task LoadAsync(CancellationToken ct = default)
  {
    IsLoading     = true;
    StatusMessage = string.Empty;

    while (!ct.IsCancellationRequested)
    {
      try
      {
        var caps = await _controller.GetCapabilitiesAsync(ct);

        foreach (var p in Parameters)
          p.PropertyChanged -= OnParameterPropertyChanged;

        Parameters.Clear();
        foreach (var p in caps.Parameters)
          Parameters.Add(p);

        Commands.Clear();
        foreach (var c in caps.Commands)
          Commands.Add(c);

        if (Parameters.Count == 0 && Commands.Count == 0)
          StatusMessage = "No capabilities reported by the grabber.";

        break;
      }
      catch (OperationCanceledException)
      {
        break;
      }
      catch (Exception ex)
      {
        _log.Warning(this, $"GetCapabilities failed, retrying in 2s: {ex.Message}");
        StatusMessage = "Waiting for grabber service...";

        try { await Task.Delay(2000, ct); }
        catch (OperationCanceledException) { break; }
      }
    }

    IsLoading = false;
  }

  // ── 획득 제어 ────────────────────────────────────────────

  [RelayCommand]
  private async Task StartAcquisition()
  {
    await Execute(async () =>
    {
      await _controller.StartAcquisitionAsync();
      _log.Info(this, "StartAcquisition");
    });
  }

  [RelayCommand]
  private async Task StopAcquisition()
  {
    await Execute(async () =>
    {
      await _controller.StopAcquisitionAsync();
      _log.Info(this, "StopAcquisition");
    });
  }

  [RelayCommand(CanExecute = nameof(IsTriggerMode))]
  private async Task TriggerFrame()
  {
    await Execute(async () =>
    {
      await _controller.TriggerFrameAsync();
      _log.Info(this, "TriggerFrame");
    });
  }

  // ── Apply / Restore ──────────────────────────────────────

  [RelayCommand]
  private async Task Apply()
  {
    var modified = Parameters.Where(p => p.IsModified).ToList();
    if (modified.Count == 0) return;

    await Execute(async () =>
    {
      foreach (var p in modified)
      {
        _controller.SetProperty(p.Key, p.CurrentValue);
        p.OriginalValue = p.CurrentValue;
      }
    });
  }

  [RelayCommand]
  private void Restore()
  {
    Execute(() =>
    {
      foreach (var p in Parameters)
        p.CurrentValue = p.OriginalValue;
    });
  }

  // ── Command 실행 ─────────────────────────────────────────

  [RelayCommand]
  private async Task ExecuteCommand(GrabberCommandItem command)
  {
    await Execute(async () =>
    {
      await _controller.ExecuteCommandAsync(command.Key);
      _log.Info(this, $"ExecuteCommand: {command.Key}");
    });
  }

  // ── OpticSettings 연동 ───────────────────────────────────

  public void SetProperty(string key, object? value) =>
      _controller.SetProperty(key, value);

  // ── acquisition_mode 감지 ─────────────────────────────────

  partial void OnIsTriggerModeChanged(bool value) =>
      TriggerFrameCommand.NotifyCanExecuteChanged();

  private void OnParametersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
  {
    if (e.NewItems is not null)
      foreach (GrabberParameterItem item in e.NewItems)
        item.PropertyChanged += OnParameterPropertyChanged;

    if (e.OldItems is not null)
      foreach (GrabberParameterItem item in e.OldItems)
        item.PropertyChanged -= OnParameterPropertyChanged;

    RefreshTriggerMode();
  }

  private void OnParameterPropertyChanged(object? sender, PropertyChangedEventArgs e)
  {
    if (sender is GrabberParameterItem { Key: "acquisition_mode" } &&
        e.PropertyName == nameof(GrabberParameterItem.CurrentValue))
      RefreshTriggerMode();
  }

  private void RefreshTriggerMode()
  {
    var modeItem = Parameters.FirstOrDefault(p => p.Key == "acquisition_mode");
    IsTriggerMode = modeItem?.CurrentValue is string s && s == "Triggered";
  }

  // ── 유틸리티 ─────────────────────────────────────────────

  private void Execute(System.Action action) => base.Execute(action);

  private async Task Execute(System.Func<Task> action,
      [System.Runtime.CompilerServices.CallerMemberName] string method = "")
  {
    _log.Debug(this, $"→ {method}");
    try
    {
      await action();
    }
    catch (System.Exception ex)
    {
      _log.Error(this, $"{method} failed: {ex.Message}");
      throw;
    }
    _log.Debug(this, $"← {method}");
  }
}
