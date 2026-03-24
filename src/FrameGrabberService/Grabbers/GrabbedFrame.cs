namespace FrameGrabberService.Grabbers;

public enum PixelFormat { Mono8, Rgb8, Bgr8 }

public record GrabbedFrame(
    string          FrameId,
    byte[]          PixelData,
    int             Width,
    int             Height,
    PixelFormat     PixelFormat,
    int             Stride,
    DateTimeOffset  Timestamp);
