using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core.Models;
using InspectionClient.Infrastructure;
using InspectionClient.Infrastructure.Entities;
using InspectionClient.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace InspectionClient.Repositories;

public sealed class UserAnnotationRepository : IUserAnnotationRepository {
  private readonly InspectionDbContext _db;

  public UserAnnotationRepository(InspectionDbContext db) {
    _db = db;
  }

  public async Task SaveAsync(UserAnnotation annotation, CancellationToken ct = default) {
    ArgumentNullException.ThrowIfNull(annotation);

    var entity = new UserAnnotationEntity {
      EntityId = annotation.EntityId,
      EntityKind = annotation.EntityKind.ToString(),
      Operator = annotation.Operator,
      Comment = annotation.Comment,
      Tags = string.Join(",", annotation.Tags),
      CreatedAt = annotation.CreatedAt.ToString("O"),
    };

    _db.UserAnnotations.Add(entity);
    await _db.SaveChangesAsync(ct);
  }

  public async Task<IReadOnlyList<UserAnnotationEntry>> ListAsync(
      string entityId, EntityKind kind, CancellationToken ct = default) {
    ArgumentException.ThrowIfNullOrWhiteSpace(entityId);

    var kindStr = kind.ToString();
    var entities = await _db.UserAnnotations.AsNoTracking()
        .Where(a => a.EntityId == entityId && a.EntityKind == kindStr)
        .OrderByDescending(a => a.CreatedAt)
        .ToListAsync(ct);

    return entities.Select(e => {
      var tags = e.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries);
      var annotation = new UserAnnotation(
        EntityId: e.EntityId,
        EntityKind: Enum.Parse<EntityKind>(e.EntityKind),
        Operator: e.Operator,
        Comment: e.Comment,
        Tags: tags,
        CreatedAt: DateTimeOffset.Parse(e.CreatedAt));
      return new UserAnnotationEntry(e.Id, annotation);
    }).ToList();
  }

  public async Task DeleteAsync(long id, CancellationToken ct = default) {
    var entity = await _db.UserAnnotations.FindAsync([id], ct);
    if (entity is not null) {
      _db.UserAnnotations.Remove(entity);
      await _db.SaveChangesAsync(ct);
    }
  }
}
