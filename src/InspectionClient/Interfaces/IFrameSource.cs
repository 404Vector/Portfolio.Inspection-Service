using System;
using Avalonia.Media.Imaging;

namespace InspectionClient.Interfaces;

/// <summary>
/// 프레임을 공급하는 소스의 추상화.
/// 구현체는 배경 스레드에서 픽셀을 쓴 후 UI 스레드에서 버퍼를 교체한다.
/// ViewModel은 FrameSwapped를 구독해 DisplayControl.Source를 갱신한다.
/// </summary>
public interface IFrameSource
{
  /// <summary>
  /// UI 스레드에서 읽기 버퍼 교체가 완료된 후 발생한다.
  /// 구독자는 UI 작업을 직접 수행할 수 있다.
  /// </summary>
  event EventHandler<WriteableBitmap> FrameSwapped;

  /// <summary>프레임 구독 루프를 시작한다.</summary>
  void Start();

  /// <summary>프레임 구독 루프를 중단한다.</summary>
  void Stop();
}
