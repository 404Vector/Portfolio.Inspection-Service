using Core.Enums;

namespace Core.Interfaces;

public interface IInspectionResult
{
    string           FrameId     { get; }
    InspectionStatus Status      { get; }
    DateTimeOffset   InspectedAt { get; }
}
