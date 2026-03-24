using DomainPixelFormat = FrameGrabberService.Grabbers.PixelFormat;

namespace FrameGrabberService.SharedMemory;

public class RingBufferOptions
{
    public string            Name        { get; set; } = "fgs_ringbuffer";
    public int               SlotCount   { get; set; } = 8;
    public int               Width       { get; set; } = 1280;
    public int               Height      { get; set; } = 1024;
    public DomainPixelFormat PixelFormat { get; set; } = DomainPixelFormat.Mono8;
}
