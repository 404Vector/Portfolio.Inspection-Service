using System.Threading;
using System.Threading.Tasks;

namespace InspectionClient.Interfaces;

/// <summary>
/// 알려진 서비스 키 상수. probe 구현체와 ViewModel이 동일한 키를 참조한다.
/// probe 구현체가 없는 서비스(미구현)의 키는 여기에 정의한다.
/// </summary>
public static class ServiceKeys
{
  public const string Inspection = "Inspection";
}

/// <summary>
/// 단일 엔드포인트의 연결 가능 여부를 확인하는 probe.
/// k8s의 livenessProbe / readinessProbe에 대응한다.
/// </summary>
public interface IConnectionProbe
{
  /// <summary>
  /// 헤더 상태 표시기에 사용할 서비스 식별자.
  /// 예: "FrameGrabber", "Inspection"
  /// </summary>
  string ServiceKey { get; }

  /// <summary>
  /// 엔드포인트에 접근을 시도한다.
  /// true = 정상 응답, false = 응답 없음 또는 오류.
  /// 구현체는 예외를 던지지 않고 false를 반환한다.
  /// </summary>
  Task<bool> CheckAsync(CancellationToken ct);
}
