using System;

namespace InspectionClient.Infrastructure.Entities;

public sealed class UserAnnotationEntity {
  public long Id { get; set; }
  public string EntityId { get; set; } = string.Empty;
  public string EntityKind { get; set; } = string.Empty;
  public string Operator { get; set; } = string.Empty;
  public string Comment { get; set; } = string.Empty;
  public string Tags { get; set; } = string.Empty;
  public string CreatedAt { get; set; } = string.Empty;
}
