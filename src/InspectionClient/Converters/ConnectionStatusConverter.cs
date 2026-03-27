using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace InspectionClient.Converters;

/// <summary>
/// bool → IBrush 변환. true = StatusConnected, false = StatusOffline.
/// XAML에서 정적 인스턴스로 참조한다.
/// </summary>
public sealed class ConnectionStatusConverter : IValueConverter
{
  public static readonly ConnectionStatusConverter Instance = new();

  public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
  {
    var connected = value is true;
    var key = connected ? "StatusConnected" : "StatusOffline";

    if (Avalonia.Application.Current?.TryGetResource(key, null, out var resource) == true
        && resource is IBrush brush)
    {
      return brush;
    }

    return connected
        ? new SolidColorBrush(Color.Parse("#9ECE6A"))
        : new SolidColorBrush(Color.Parse("#565F89"));
  }

  public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
      => throw new NotSupportedException();
}
