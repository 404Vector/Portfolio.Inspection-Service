namespace FrameGrabberService.Grabbers;

public enum GrabberState { Idle, Acquiring, Error }

public record GrabberStatus(
    GrabberState    State,
    AcquisitionMode Mode,
    long            FramesGrabbed,
    string?         Message = null);
