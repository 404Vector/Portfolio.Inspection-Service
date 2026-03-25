using FrameGrabberService.Models;

namespace FrameGrabberService.Interfaces;

public interface IFrameGrabber : IAsyncDisposable
{
    GrabberStatus GetStatus();

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
}
