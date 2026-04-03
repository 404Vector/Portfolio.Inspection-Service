using System;

namespace InspectionClient.Infrastructure.Entities;

public sealed class InspectionResultEntity {
  public string ResultId { get; set; } = string.Empty;
  public string RecipeName { get; set; } = string.Empty;
  public string WaferId { get; set; } = string.Empty;
  public string Status { get; set; } = string.Empty;
  public string StartedAt { get; set; } = string.Empty;
  public string CompletedAt { get; set; } = string.Empty;
  public string Json { get; set; } = string.Empty;
}
