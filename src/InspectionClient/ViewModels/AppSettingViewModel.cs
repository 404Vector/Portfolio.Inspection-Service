using System;
using System.Collections.Generic;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using InspectionClient.Enums;
using InspectionClient.Interfaces;

namespace InspectionClient.ViewModels;

public partial class AppSettingViewModel : ViewModelBase
{
    private readonly ILogService _log;
    private Timer? _logTimer;

    public IReadOnlyList<LogLevel> LogLevels { get; } = Enum.GetValues<LogLevel>();

    [ObservableProperty] private bool     _isLogTestRunning;
    [ObservableProperty] private LogLevel _selectedLogLevel = LogLevel.Info;

    public AppSettingViewModel(ILogService logService)
    {
        _log = logService;
    }

    partial void OnIsLogTestRunningChanged(bool value)
    {
        if (value) StartLogTest();
        else       StopLogTest();
    }

    private void StartLogTest()
    {
        _logTimer = new Timer(_ =>
        {
            var hash = DateTimeOffset.Now.GetHashCode().ToString("X8");
            _log.Log(this, SelectedLogLevel, $"{hash}");
        }, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(100));
    }

    private void StopLogTest()
    {
        _logTimer?.Dispose();
        _logTimer = null;
    }
}
