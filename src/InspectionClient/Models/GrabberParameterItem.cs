using CommunityToolkit.Mvvm.ComponentModel;
using Core.Grpc.FrameGrabber;

namespace InspectionClient.Models;

/// <summary>
/// ParameterDescriptor + 현재 값을 묶은 Observable UI 모델.
/// ViewModel이 ObservableCollection에 담아 View에 바인딩한다.
/// </summary>
public partial class GrabberParameterItem : ObservableObject
{
  public string             Key         { get; init; } = "";
  public string             DisplayName { get; init; } = "";
  public ParameterValueType ValueType   { get; init; }
  public object?            MinValue    { get; init; }
  public object?            MaxValue    { get; init; }

  [ObservableProperty]
  private object? _currentValue;

  /// <summary>원래 서버 값. Apply/Restore 비교에 사용.</summary>
  public object? OriginalValue { get; set; }

  public bool IsModified => !Equals(CurrentValue, OriginalValue);
}
