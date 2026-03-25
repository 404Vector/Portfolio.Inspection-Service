using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using InspectionClient.Enums;

namespace InspectionClient.Converters;

/// <summary>
/// LogLevel → IBrush 변환. Colors.axaml 팔레트 색상과 동기화.
/// </summary>
public sealed class LogLevelToForegroundConverter : IValueConverter
{
    // Colors.axaml 기준
    private static readonly IBrush LogTrace   = new SolidColorBrush(Color.Parse("#FF9E64"));
    private static readonly IBrush LogDebug   = new SolidColorBrush(Color.Parse("#6B7498"));
    private static readonly IBrush LogInfo    = new SolidColorBrush(Color.Parse("#C0CAF5"));
    private static readonly IBrush LogWarning = new SolidColorBrush(Color.Parse("#E0AF68"));
    private static readonly IBrush LogError   = new SolidColorBrush(Color.Parse("#F7768E"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is LogLevel level ? level switch
        {
            LogLevel.Trace   => LogTrace,
            LogLevel.Debug   => LogDebug,
            LogLevel.Info    => LogInfo,
            LogLevel.Warning => LogWarning,
            LogLevel.Error   => LogError,
            _                => LogInfo,
        } : LogInfo;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
