using System;

namespace InspectionClient.Infrastructure.Entities;

public sealed class DieSpotRecipeEntity {
  public long Id { get; set; }
  public string Name { get; set; } = string.Empty;
  public string CreatedAt { get; set; } = string.Empty;
  public string Json { get; set; } = string.Empty;
}
