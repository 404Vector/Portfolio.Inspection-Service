using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Core.Grpc.FrameGrabber;
using Core.Logging.Interfaces;
using InspectionClient.Interfaces;
using InspectionClient.Models;
using InspectionClient.Services.Probes;

namespace InspectionClient.ViewModels;

public abstract partial class FrameGrabberViewModelBase : ViewModelBase
{
  private readonly IFrameGrabberController _controller;
  private readonly IFrameSource _frameSource;

  protected IFrameGrabberController Controller => _controller;

  public ObservableCollection<GrabberParameterItem> Parameters { get; }

  [ObservableProperty]
  private WriteableBitmap? _frameImage;

  [ObservableProperty]
  private bool _isLoading;

  [ObservableProperty]
  private string _statusMessage = string.Empty;

  /// <summary>true이면 Triggered 모드 — Trigger 버튼을 활성화한다.</summary>
  [ObservableProperty]
  private bool _isTriggerMode;

  [ObservableProperty]
  private bool _isConnected;

  [ObservableProperty]
  private bool _isAcquisitionRunning;

  protected FrameGrabberViewModelBase(
      ILogService logService,
      IFrameGrabberController controller,
      IFrameSource frameSource,
      IServiceConnectionMonitor connectionMonitor,
      IEnumerable<GrabberParameterItem> parameters)
      : base(logService)
  {
    _controller = controller;
    _frameSource = frameSource;
    Parameters = new ObservableCollection<GrabberParameterItem>(parameters);

    _frameSource.FrameSwapped += (_, bitmap) => FrameImage = bitmap;
    _frameSource.Start();

    foreach (var item in Parameters)
      item.PropertyChanged += OnParameterPropertyChanged;

    Parameters.CollectionChanged += OnParametersCollectionChanged;
    RefreshTriggerMode();

    // Probe가 시작되기 전에도 서비스가 실행 중이면 즉시 동기화
    _ = LoadAsync();

    // Probe 기반 연결 변화 감지: 재연결 시 재동기화, 끊김 시 패널 비활성화
    connectionMonitor.StateChanged += (_, e) =>
    {
      if (e.ServiceKey != GrpcFrameGrabberProbe.Key) return;
      if (!e.IsConnected)
        IsConnected = false;
      else
        _ = LoadAsync();
    };
  }

  // ── 초기화 ─────────────────────────────────────────────────

  /// <summary>서버로부터 현재 파라미터 값을 동기화한다.</summary>
  public async Task LoadAsync(CancellationToken ct = default)
  {
    IsLoading = true;
    StatusMessage = string.Empty;

    try
    {
      var status = await _controller.GetStatusAsync(ct);
      IsAcquisitionRunning = status.State == GrabberState.Acquiring;

      var caps = await _controller.GetCapabilitiesAsync(ct);

      foreach (var item in Parameters)
      {
        var serverParam = caps.Parameters.FirstOrDefault(p => p.Key == item.Key);
        if (serverParam is null) continue;

        var current = await _controller.GetParameterAsync(item.Key, ct)
            ?? serverParam.CurrentValue;

        item.CurrentValue  = current;
        item.OriginalValue = current;
      }

      RefreshTriggerMode();
      IsConnected = true;
    }
    catch (OperationCanceledException) { }
    catch (Exception ex)
    {
      _log.Warning(this, $"LoadAsync failed: {ex.Message}");
      StatusMessage = "Failed to sync grabber parameters.";
    }
    finally
    {
      IsLoading = false;
    }
  }

  // ── 획득 제어 ───────────────────────────────────────────────

  public bool CanTrigger => IsAcquisitionRunning && IsTriggerMode;

  [RelayCommand]
  private async Task StartAcquisition() =>
      await Execute(async () =>
      {
        await _controller.StartAcquisitionAsync();
        IsAcquisitionRunning = true;
        _log.Info(this, "StartAcquisition");
      });

  [RelayCommand]
  private async Task StopAcquisition() =>
      await Execute(async () =>
      {
        await _controller.StopAcquisitionAsync();
        IsAcquisitionRunning = false;
        _log.Info(this, "StopAcquisition");
      });

  [RelayCommand(CanExecute = nameof(CanTrigger))]
  private async Task TriggerFrame() =>
      await Execute(async () =>
      {
        await _controller.TriggerFrameAsync();
        _log.Info(this, "TriggerFrame");
      });

  // ── Apply / Restore ────────────────────────────────────────

  [RelayCommand]
  private async Task Apply()
  {
    var modified = Parameters.Where(p => p.IsModified).ToList();
    if (modified.Count == 0) return;

    await Execute(async () =>
    {
      foreach (var p in modified)
      {
        bool success;
        string message;

        if (p.ValueType == ParameterValueType.Bytes && p.BytesData is not null)
        {
          (success, message) = await _controller.SetParameterWithStreamAsync(
              p.Key, p.BytesData);
        }
        else
        {
          (success, message) = await _controller.SetParameterAsync(
              p.Key, CoerceToValueType(p.CurrentValue, p.ValueType));
        }

        if (success)
        {
          p.OriginalValue = p.CurrentValue;
          if (p.ValueType == ParameterValueType.Bytes)
            p.BytesData = null;
        }
        else
        {
          p.CurrentValue = p.OriginalValue;
          _log.Warning(this, $"SetParameter '{p.Key}' rejected: {message}");
        }
      }
    });
  }

  [RelayCommand]
  private void Restore() =>
      Execute(() =>
      {
        foreach (var p in Parameters)
          p.CurrentValue = p.OriginalValue;
      });

  // ── 파일 선택 (Bytes 파라미터) ──────────────────────────────

  [RelayCommand]
  private async Task BrowseFile(GrabberParameterItem parameter)
  {
    if (parameter.ValueType != ParameterValueType.Bytes) return;

    var topLevel = Avalonia.Application.Current?.ApplicationLifetime
        is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
        ? desktop.MainWindow : null;
    if (topLevel is null) return;

    var files = await topLevel.StorageProvider.OpenFilePickerAsync(
        new FilePickerOpenOptions
        {
          Title = $"Select {parameter.DisplayName}",
          AllowMultiple = false,
          FileTypeFilter =
          [
            new FilePickerFileType("Image Files") { Patterns = ["*.png", "*.bmp", "*.jpg", "*.jpeg", "*.tif", "*.tiff", "*.raw"] },
            FilePickerFileTypes.All,
          ]
        });

    if (files.Count == 0) return;

    var file = files[0];
    await using var stream = await file.OpenReadAsync();
    using var ms = new MemoryStream();
    await stream.CopyToAsync(ms);

    parameter.BytesData    = ms.ToArray();
    parameter.CurrentValue = file.Name;

    _log.Info(this, $"File selected for '{parameter.Key}': {file.Name} ({parameter.BytesData.Length:N0} bytes)");
  }

  // ── OpticSettings 연동 ─────────────────────────────────────

  public void SetProperty(string key, object? value) =>
      _controller.SetProperty(key, value);

  // ── acquisition_mode 감지 ──────────────────────────────────

  partial void OnIsAcquisitionRunningChanged(bool value) =>
      TriggerFrameCommand.NotifyCanExecuteChanged();

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

  // ── 유틸리티 ──────────────────────────────────────────────

  private static object? CoerceToValueType(object? value, ParameterValueType valueType) =>
      (value, valueType) switch
      {
        (decimal m, ParameterValueType.Int64)  => (long)m,
        (decimal m, ParameterValueType.Double) => (double)m,
        _                                      => value,
      };
}
