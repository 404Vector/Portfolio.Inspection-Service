using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Core.Models;
using InspectionClient.Infrastructure;
using InspectionClient.Infrastructure.Entities;
using InspectionClient.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace InspectionClient.Repositories;

public sealed class InspectionResultRepository : IInspectionResultRepository {
  private readonly InspectionDbContext _db;

  public InspectionResultRepository(InspectionDbContext db) {
    _db = db;
  }

  public async Task<string> SaveAsync(WaferSurfaceInspectionResult result, CancellationToken ct = default) {
    ArgumentNullException.ThrowIfNull(result);

    var resultId = Guid.NewGuid().ToString();
    var entity = new InspectionResultEntity {
      ResultId = resultId,
      RecipeName = result.RecipeName,
      WaferId = result.WaferId,
      Status = result.Status.ToString(),
      StartedAt = result.StartedAt.ToString("O"),
      CompletedAt = result.CompletedAt.ToString("O"),
      Json = JsonSerializer.Serialize(result, RepositoryJsonOptions.Default),
    };

    _db.InspectionResults.Add(entity);
    await _db.SaveChangesAsync(ct);
    return resultId;
  }

  public async Task<WaferSurfaceInspectionResult?> FindAsync(string resultId, CancellationToken ct = default) {
    ArgumentException.ThrowIfNullOrWhiteSpace(resultId);

    var entity = await _db.InspectionResults.AsNoTracking()
        .FirstOrDefaultAsync(r => r.ResultId == resultId, ct);
    if (entity is null)
      return null;

    return JsonSerializer.Deserialize<WaferSurfaceInspectionResult>(
        entity.Json, RepositoryJsonOptions.Default);
  }

  public async Task<IReadOnlyList<WaferSurfaceInspectionResult>> ListAsync(CancellationToken ct = default) {
    var entities = await _db.InspectionResults.AsNoTracking()
        .OrderByDescending(r => r.StartedAt)
        .ToListAsync(ct);

    return entities
        .Select(e => JsonSerializer.Deserialize<WaferSurfaceInspectionResult>(
            e.Json, RepositoryJsonOptions.Default)!)
        .ToList();
  }

  public async Task<IReadOnlyList<InspectionResultEntry>> ListEntriesAsync(CancellationToken ct = default) {
    var entities = await _db.InspectionResults.AsNoTracking()
        .OrderByDescending(r => r.StartedAt)
        .ToListAsync(ct);

    return entities
        .Select(e => {
          var item = JsonSerializer.Deserialize<WaferSurfaceInspectionResult>(
              e.Json, RepositoryJsonOptions.Default)!;
          return new InspectionResultEntry(e.ResultId, item);
        })
        .ToList();
  }

  public async Task DeleteAsync(string resultId, CancellationToken ct = default) {
    ArgumentException.ThrowIfNullOrWhiteSpace(resultId);

    var entity = await _db.InspectionResults.FindAsync([resultId], ct);
    if (entity is not null) {
      _db.InspectionResults.Remove(entity);
      await _db.SaveChangesAsync(ct);
    }
  }
}
