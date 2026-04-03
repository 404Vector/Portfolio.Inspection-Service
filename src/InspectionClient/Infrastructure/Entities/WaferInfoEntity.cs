using System;

namespace InspectionClient.Infrastructure.Entities;

public sealed class WaferInfoEntity {
  public long Id { get; set; }
  public string Name { get; set; } = string.Empty;
  public string WaferType { get; set; } = string.Empty;
  public string CreatedAt { get; set; } = string.Empty;
  public long? DieParametersId { get; set; }
  public string Json { get; set; } = string.Empty;

  public DieRenderingParametersEntity? DieParameters { get; set; }
}
