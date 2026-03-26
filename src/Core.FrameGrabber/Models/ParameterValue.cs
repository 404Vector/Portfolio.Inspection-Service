namespace Core.FrameGrabber.Models;

/// <summary>
/// 파라미터의 런타임 값. 정확히 하나의 typed value를 담는 discriminated union.
/// </summary>
public abstract record ParameterValue
{
    public sealed record Int64Value (long   Value) : ParameterValue;
    public sealed record DoubleValue(double Value) : ParameterValue;
    public sealed record BoolValue  (bool   Value) : ParameterValue;
    public sealed record StringValue(string Value) : ParameterValue;
}
