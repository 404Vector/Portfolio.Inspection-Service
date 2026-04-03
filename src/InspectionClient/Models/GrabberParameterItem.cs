using System.Collections.Generic;
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

  /// <summary>enum 타입의 경우, 허용되는 값 목록.</summary>
  public List<string>? AllowedValues { get; set; }

  [ObservableProperty]
  private object? _currentValue;

  /// <summary>원래 서버 값. Apply/Restore 비교에 사용.</summary>
  public object? OriginalValue { get; set; }

  /// <summary>
  /// Bytes 타입 파라미터의 바이너리 데이터.
  /// 파일 선택 시 읽어 들인 byte[]를 보관하고, Apply 시 스트리밍 전송에 사용한다.
  /// CurrentValue에는 파일 이름(표시용)을 저장한다.
  /// </summary>
  public byte[]? BytesData { get; set; }

  public bool IsModified => ValueType == ParameterValueType.Bytes
      ? BytesData is not null
      : !Equals(CurrentValue, OriginalValue);
}
