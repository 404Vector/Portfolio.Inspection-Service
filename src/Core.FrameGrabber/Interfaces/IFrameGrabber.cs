using Core.FrameGrabber.Models;

namespace Core.FrameGrabber.Interfaces;

public interface IFrameGrabber : IAsyncDisposable
{
    // ── 상태 ─────────────────────────────────────────────────────────────────

    GrabberStatus GetStatus();

    // ── 획득 제어 ─────────────────────────────────────────────────────────────

    Task ConfigureAsync(GrabberConfig config, CancellationToken ct = default);

    /// <summary>
    /// Starts acquisition. In Continuous mode launches the background capture loop.
    /// In Triggered mode simply transitions to Acquiring state.
    /// </summary>
    Task StartAsync(CancellationToken ct = default);

    Task StopAsync(CancellationToken ct = default);

    /// <summary>
    /// Grabs one frame immediately. Works in both modes.
    /// The frame is also published to <see cref="GetFramesAsync"/> subscribers.
    /// </summary>
    Task<GrabbedFrame> TriggerAsync(CancellationToken ct = default);

    /// <summary>
    /// Async stream of all grabbed frames (continuous + triggered).
    /// </summary>
    IAsyncEnumerable<GrabbedFrame> GetFramesAsync(CancellationToken ct);

    // ── 동적 파라미터 ─────────────────────────────────────────────────────────

    /// <summary>
    /// 이 구현체가 지원하는 파라미터 목록을 반환한다.
    /// </summary>
    IReadOnlyList<ParameterDescriptor> GetParameters();

    /// <summary>
    /// 지정한 파라미터의 현재 값을 반환한다.
    /// </summary>
    /// <exception cref="KeyNotFoundException">key가 존재하지 않을 때</exception>
    ParameterValue GetParameter(string key);

    /// <summary>
    /// 지정한 파라미터 값을 설정한다.
    /// </summary>
    /// <exception cref="KeyNotFoundException">key가 존재하지 않을 때</exception>
    /// <exception cref="ArgumentException">value 타입이 맞지 않거나 범위를 벗어날 때</exception>
    Task SetParameterAsync(string key, ParameterValue value, CancellationToken ct = default);

    // ── 동적 명령 ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 이 구현체가 지원하는 명령 목록을 반환한다.
    /// </summary>
    IReadOnlyList<CommandDescriptor> GetCommands();

    /// <summary>
    /// 지정한 명령을 실행한다.
    /// </summary>
    /// <exception cref="KeyNotFoundException">command가 존재하지 않을 때</exception>
    Task<CommandResult> ExecuteCommandAsync(string command, CancellationToken ct = default);
}
