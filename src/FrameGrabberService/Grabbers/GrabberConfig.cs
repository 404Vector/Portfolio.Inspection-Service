namespace FrameGrabberService.Grabbers;

public record GrabberConfig(
    Core.Enums.AcquisitionMode Mode         = Core.Enums.AcquisitionMode.Continuous,
    Core.Enums.PixelFormat     PixelFormat  = Core.Enums.PixelFormat.Mono8,
    int                        Width        = 1280,
    int                        Height       = 1024,
    double                     FrameRateHz  = 30.0)
{
    public static readonly GrabberConfig Default = new();
}
