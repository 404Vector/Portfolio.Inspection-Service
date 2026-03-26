using System;
using System.Collections.Generic;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Core.Logging.Enums;
using Core.Logging.Interfaces;

namespace InspectionClient.ViewModels;

public partial class AppSettingViewModel : ViewModelBase
{
    private Timer? _logTimer;

    public IReadOnlyList<LogLevel> LogLevels { get; } = Enum.GetValues<LogLevel>();

    [ObservableProperty] private bool     _isLogTestRunning;
    [ObservableProperty] private LogLevel _selectedLogLevel = LogLevel.Info;

    public AppSettingViewModel(ILogService logService) : base(logService) { }

    partial void OnIsLogTestRunningChanged(bool value)
    {
        if (value) StartLogTest();
        else       StopLogTest();
    }

    private void StartLogTest() => Execute(() =>
    {
        _logTimer = new Timer(_ =>
        {
            var hash = DateTimeOffset.Now.GetHashCode().ToString("X8");
            _log.Log(this, SelectedLogLevel, $"{hash}");
        }, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(100));
    });

    private void StopLogTest() => Execute(() =>
    {
        _logTimer?.Dispose();
        _logTimer = null;
    });
}
