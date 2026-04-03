using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace InspectionClient.Converters;

/// <summary>
/// bool → 문자열 변환. TrueText / FalseText를 설정하여 사용한다.
/// </summary>
public sealed class BoolToTextConverter : IValueConverter
{
  public string TrueText  { get; set; } = "True";
  public string FalseText { get; set; } = "False";

  public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
      value is true ? TrueText : FalseText;

  public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
      throw new NotSupportedException();
}
