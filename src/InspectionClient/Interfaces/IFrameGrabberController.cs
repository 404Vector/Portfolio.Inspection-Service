using System.Threading;
using System.Threading.Tasks;
using InspectionClient.Models;

namespace InspectionClient.Interfaces;

/// <summary>
/// 원격 FrameGrabber에 대한 제어 추상화.
/// 획득 생명주기, 파라미터 설정, 동적 파라미터/커맨드 조회를 담당한다.
/// </summary>
public interface IFrameGrabberController
{
  // ── 획득 제어 ────────────────────────────────────────────

  /// <summary>FrameGrabber 획득을 시작한다.</summary>
  Task StartAcquisitionAsync(CancellationToken ct = default);

  /// <summary>FrameGrabber 획득을 중지한다.</summary>
  Task StopAcquisitionAsync(CancellationToken ct = default);

  /// <summary>SW 트리거로 프레임 1개를 즉시 캡처한다.</summary>
  Task TriggerFrameAsync(CancellationToken ct = default);

  // ── 파라미터 설정 ─────────────────────────────────────────

  /// <summary>
  /// 런타임 FrameGrabber 속성을 설정한다.
  /// key는 OpticSettings의 속성명(nameof)을 사용한다.
  /// 구현체가 지원하지 않는 key는 무시한다.
  /// </summary>
  void SetProperty(string key, object? value);

  // ── 동적 파라미터 / 커맨드 ──────────────────────────────

  /// <summary>
  /// FrameGrabber가 노출하는 동적 파라미터/커맨드 목록을 조회한다.
  /// 구현체가 지원하지 않으면 빈 GrabberCapabilities를 반환한다.
  /// </summary>
  Task<GrabberCapabilities> GetCapabilitiesAsync(CancellationToken ct = default);

  /// <summary>FrameGrabber 커맨드를 실행한다.</summary>
  Task ExecuteCommandAsync(string commandKey, CancellationToken ct = default);
}
