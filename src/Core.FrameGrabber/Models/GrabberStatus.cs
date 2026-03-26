namespace Core.FrameGrabber.Models;

public record GrabberStatus(
    Core.Enums.GrabberState    State,
    Core.Enums.AcquisitionMode Mode,
    long                       FramesGrabbed,
    string?                    Message = null);
