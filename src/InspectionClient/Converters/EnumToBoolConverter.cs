using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace InspectionClient.Converters;

/// <summary>
/// RadioButton IsChecked 바인딩용. ConverterParameter와 값이 일치하면 true.
/// </summary>
public class EnumToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value?.Equals(parameter) ?? false;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? parameter : null;
}
