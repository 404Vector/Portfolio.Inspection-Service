namespace Core.Exceptions;

public class InspectionException : Exception
{
    public InspectionException(string message) : base(message) { }
    public InspectionException(string message, Exception inner) : base(message, inner) { }
}
