namespace Core.Models;

/// <summary>
/// UserAnnotation이 연결된 엔티티 종류.
/// </summary>
public enum EntityKind
{
  /// <summary>WaferInfo 엔티티.</summary>
  WaferInfo,

  /// <summary>WaferSurfaceInspectionRecipe 엔티티.</summary>
  Recipe,

  /// <summary>WaferSurfaceInspectionResult 엔티티.</summary>
  InspectionResult,
}
