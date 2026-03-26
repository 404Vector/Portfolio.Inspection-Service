using System.Collections.Generic;
using System.Reflection;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using Core.Logging.Interfaces;
using InspectionClient.Interfaces;
using InspectionClient.Models;

namespace InspectionClient.ViewModels;

public partial class OpticSettingViewModel : ViewModelBase
{
    private readonly IFrameSource _frameSource;

    public OpticSettings Settings    { get; private set; } = new();
    public OpticSettings RealSettings { get; } = new();

    // [ObservableProperty] 대신 수동 정의 —
    // 동일 인스턴스 재할당 시 자동 알림이 누락되지 않도록 setter가 항상 OnPropertyChanged를 발생시킨다.
    private WriteableBitmap? _frameImage;
    public WriteableBitmap? FrameImage
    {
        get => _frameImage;
        private set
        {
            _frameImage = value;
            OnPropertyChanged();
        }
    }

    public OpticSettingViewModel(ILogService logService, IFrameSource frameSource) : base(logService)
    {
        _frameSource = frameSource;

        // FrameSwapped는 UI 스레드에서 발생하므로 직접 Source를 교체해도 안전하다.
        _frameSource.FrameSwapped += (_, bitmap) => FrameImage = bitmap;
        _frameSource.Start();
    }

    // ── Apply / Restore ──────────────────────────────────────

    [RelayCommand]
    private void Apply() => Execute(() =>
    {
        foreach (var prop in FindDifference(Settings, RealSettings))
        {
            var value = prop.GetValue(Settings);
            prop.SetValue(RealSettings, value);
            _frameSource.SetProperty(prop.Name, value);
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

    /// <summary>
    /// a와 b의 값이 다른 OpticSettings 속성 목록을 반환한다.
    /// </summary>
    private static IEnumerable<PropertyInfo> FindDifference(OpticSettings a, OpticSettings b)
    {
        foreach (var prop in typeof(OpticSettings).GetProperties())
        {
            if (!Equals(prop.GetValue(a), prop.GetValue(b)))
                yield return prop;
        }
    }
}
