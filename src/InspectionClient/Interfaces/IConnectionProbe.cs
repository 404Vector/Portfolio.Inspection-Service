using System.Threading;
using System.Threading.Tasks;

namespace InspectionClient.Interfaces;

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
