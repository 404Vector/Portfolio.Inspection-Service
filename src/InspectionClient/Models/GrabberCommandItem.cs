namespace InspectionClient.Models;

/// <summary>
/// CommandDescriptor의 UI 레이어 모델.
/// </summary>
public record GrabberCommandItem(
    string Key,
    string DisplayName,
    string Description);
