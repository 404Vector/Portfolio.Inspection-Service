using System.Collections.Concurrent;

namespace VirtualFrameGrabberServer.Services;

/// <summary>
/// 활성 서버 스트리밍 RPC의 CancellationTokenSource를 추적하고,
/// 앱 종료 시 모든 스트림을 일괄 취소한다.
/// </summary>
public sealed class ActiveStreamRegistry : IDisposable
{
  private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _streams = new();

  /// <summary>
  /// CTS를 레지스트리에 등록한다. 반환된 IDisposable을 dispose하면 등록이 해제된다.
  /// </summary>
  public IDisposable Register(CancellationTokenSource cts)
  {
    var id = Guid.NewGuid();
    _streams[id] = cts;
    return new Registration(() => _streams.TryRemove(id, out _));
  }

  /// <summary>
  /// 등록된 모든 스트림을 취소한다. 앱 종료 시 호출한다.
  /// 요청 스레드가 동시에 정리 중인 경우 ObjectDisposedException을 무시한다.
  /// </summary>
  public void CancelAll()
  {
    foreach (var cts in _streams.Values)
    {
      try
      {
        cts.Cancel();
      }
      catch (ObjectDisposedException)
      {
        // 이미 요청 스레드가 정리함, 안전하게 무시
      }
    }
  }

  public void Dispose() => CancelAll();

  // ── Internals ────────────────────────────────────────────────────────────

  private sealed class Registration : IDisposable
  {
    private readonly Action _onDispose;

    internal Registration(Action onDispose) { _onDispose = onDispose; }

    public void Dispose() => _onDispose();
  }
}
