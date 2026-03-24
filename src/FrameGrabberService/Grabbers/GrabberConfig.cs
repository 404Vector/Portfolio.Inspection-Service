namespace FrameGrabberService.Grabbers;

public enum AcquisitionMode { Continuous, Triggered }

public record GrabberConfig(
    AcquisitionMode Mode         = AcquisitionMode.Continuous,
    PixelFormat     PixelFormat  = PixelFormat.Mono8,
    int             Width        = 1280,
    int             Height       = 1024,
    double          FrameRateHz  = 30.0)
{
    public static readonly GrabberConfig Default = new();
}
