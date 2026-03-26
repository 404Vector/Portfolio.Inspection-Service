namespace Core.FrameGrabber.Models;

public record GrabbedFrame(
    string                FrameId,
    byte[]                PixelData,
    int                   Width,
    int                   Height,
    Core.Enums.PixelFormat PixelFormat,
    int                   Stride,
    DateTimeOffset        Timestamp);
