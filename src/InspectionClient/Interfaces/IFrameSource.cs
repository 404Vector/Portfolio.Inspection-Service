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

    void Start();
    void Stop();

    /// <summary>
    /// 런타임 프레임 소스 속성을 설정한다.
    /// key는 OpticSettings의 속성명(nameof)을 사용한다.
    /// 구현체가 지원하지 않는 key는 무시한다.
    /// </summary>
    void SetProperty(string key, object? value);
}
