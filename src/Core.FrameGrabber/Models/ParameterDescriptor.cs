namespace Core.FrameGrabber.Models;

public enum ParameterValueType { Int64, Double, Bool, String, Bytes }

/// <summary>
/// IFrameGrabber 구현체가 외부에 노출하는 파라미터의 메타데이터.
/// </summary>
public record ParameterDescriptor(
    string             Key,
    string             DisplayName,
    ParameterValueType ValueType,
    object?            MinValue,
    object?            MaxValue,
    object?            DefaultValue)
{
    public ParameterDescriptor(string key, string displayName, ParameterValueType valueType)
        : this(key, displayName, valueType, null, null, null) { }
}
