using System;

namespace InspectionClient.Infrastructure.Entities;

public sealed class DieRenderingParametersEntity {
  public long Id { get; set; }
  public string Name { get; set; } = string.Empty;
  public string Json { get; set; } = string.Empty;
}
