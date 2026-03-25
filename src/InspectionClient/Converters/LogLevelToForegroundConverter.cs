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
    private static readonly IBrush TextMuted   = new SolidColorBrush(Color.Parse("#565F89"));
    private static readonly IBrush TextPrimary = new SolidColorBrush(Color.Parse("#C0CAF5"));
    private static readonly IBrush LogWarning  = new SolidColorBrush(Color.Parse("#E0AF68"));
    private static readonly IBrush StatusError = new SolidColorBrush(Color.Parse("#F7768E"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is LogLevel level ? level switch
        {
            LogLevel.Trace   => TextMuted,
            LogLevel.Debug   => TextMuted,
            LogLevel.Info    => TextPrimary,
            LogLevel.Warning => LogWarning,
            LogLevel.Error   => StatusError,
            _                => TextPrimary,
        } : TextPrimary;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
