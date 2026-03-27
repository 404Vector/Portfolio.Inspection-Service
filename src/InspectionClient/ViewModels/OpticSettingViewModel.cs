using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Core.Logging.Interfaces;
using InspectionClient.Interfaces;
using InspectionClient.Models;

namespace InspectionClient.ViewModels;

public partial class OpticSettingViewModel : ViewModelBase
{
  private readonly IFrameSource _frameSource;

  // ── Spec 탭 ──────────────────────────────────────────────

  public OpticSpec Spec { get; } = new();

  // ── Fg 탭 ────────────────────────────────────────────────

  public FrameGrabberControlViewModel FgViewModel { get; }

  // ── Settings (Spec 탭 하단 PropertyGrid용 편집 복사본) ───

  public OpticSettings Settings     { get; private set; } = new();
  public OpticSettings RealSettings { get; }               = new();

  // ── 프레임 이미지 ─────────────────────────────────────────

  [ObservableProperty]
  private WriteableBitmap? _frameImage;

  // ── 생성자 ────────────────────────────────────────────────

  public OpticSettingViewModel(
      ILogService logService,
      IFrameSource frameSource,
      FrameGrabberControlViewModel fgViewModel)
      : base(logService)
  {
    _frameSource = frameSource;
    FgViewModel  = fgViewModel;

    _frameSource.FrameSwapped += (_, bitmap) => FrameImage = bitmap;
    _frameSource.Start();

    _ = FgViewModel.LoadAsync();
  }

  // ── Apply / Restore (Settings 탭) ────────────────────────

  [RelayCommand]
  private void Apply() => Execute(() =>
  {
    foreach (var prop in FindDifference(Settings, RealSettings))
    {
      var value = prop.GetValue(Settings);
      prop.SetValue(RealSettings, value);
      FgViewModel.SetProperty(prop.Name, value);
    }
  });

  [RelayCommand]
  private void Restore() => Execute(() =>
  {
    var restored = new OpticSettings();
    foreach (var prop in typeof(OpticSettings).GetProperties())
      prop.SetValue(restored, prop.GetValue(RealSettings));

    Settings = restored;
    OnPropertyChanged(nameof(Settings));
  });

  // ── 유틸리티 ─────────────────────────────────────────────

  private static IEnumerable<PropertyInfo> FindDifference(OpticSettings a, OpticSettings b)
  {
    foreach (var prop in typeof(OpticSettings).GetProperties())
    {
      if (!Equals(prop.GetValue(a), prop.GetValue(b)))
        yield return prop;
    }
  }
}
