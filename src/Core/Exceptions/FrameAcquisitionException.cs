namespace Core.Exceptions;

public class FrameAcquisitionException : Exception
{
    public FrameAcquisitionException(string message) : base(message) { }
    public FrameAcquisitionException(string message, Exception inner) : base(message, inner) { }
}
