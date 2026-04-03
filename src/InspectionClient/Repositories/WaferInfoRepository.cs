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
using InspectionClient.Models;
using Microsoft.EntityFrameworkCore;

namespace InspectionClient.Repositories;

public sealed class WaferInfoRepository : IWaferInfoRepository {
  private readonly IDbContextFactory<InspectionDbContext> _factory;

  public WaferInfoRepository(IDbContextFactory<InspectionDbContext> factory) {
    _factory = factory;
  }

  public async Task<WaferInfoRow> CreateAsync(string name, CancellationToken ct = default) {
    ArgumentException.ThrowIfNullOrWhiteSpace(name);

    var info = WaferInfo.CreateDummy() with { WaferId = name };
    var json = JsonSerializer.Serialize(info, RepositoryJsonOptions.Default);

    var entity = new WaferInfoEntity {
      Name = name,
      WaferType = info.WaferType.ToString(),
      CreatedAt = info.CreatedAt.ToString("O"),
      Json = json,
    };

    await using var db = await _factory.CreateDbContextAsync(ct);
    db.WaferInfos.Add(entity);
    await db.SaveChangesAsync(ct);

    var row = new WaferInfoRow { Id = entity.Id, Name = name };
    row.LoadFrom(info);
    return row;
  }

  public async Task<WaferInfoRow?> FindByIdAsync(long id, CancellationToken ct = default) {
    await using var db = await _factory.CreateDbContextAsync(ct);
    var entity = await db.WaferInfos.AsNoTracking()
        .FirstOrDefaultAsync(r => r.Id == id, ct);
    return entity is null ? null : ToRow(entity);
  }

  public async Task<IReadOnlyList<WaferInfoRow>> ListAsync(CancellationToken ct = default) {
    await using var db = await _factory.CreateDbContextAsync(ct);
    var entities = await db.WaferInfos.AsNoTracking()
        .OrderByDescending(r => r.Id)
        .ToListAsync(ct);
    return entities.Select(ToRow).ToList();
  }

  public async Task UpdateAsync(WaferInfoRow item, CancellationToken ct = default) {
    ArgumentNullException.ThrowIfNull(item);

    await using var db = await _factory.CreateDbContextAsync(ct);
    var entity = await db.WaferInfos.FindAsync([item.Id], ct)
        ?? throw new InvalidOperationException($"WaferInfo Id={item.Id} not found.");
    var info = item.ToWaferInfo();
    entity.Name = item.Name;
    entity.WaferType = info.WaferType.ToString();
    entity.CreatedAt = info.CreatedAt.ToString("O");
    entity.DieParametersId = item.DieParametersId;
    entity.Json = JsonSerializer.Serialize(info, RepositoryJsonOptions.Default);
    await db.SaveChangesAsync(ct);
  }

  public async Task<WaferInfoRow?> FindByWaferIdAsync(string waferId, CancellationToken ct = default) {
    ArgumentException.ThrowIfNullOrWhiteSpace(waferId);

    // CreateAsync에서 Name = WaferId로 저장하므로 Name 컬럼으로 서버사이드 필터링한다.
    await using var db = await _factory.CreateDbContextAsync(ct);
    var entity = await db.WaferInfos.AsNoTracking()
        .FirstOrDefaultAsync(e => e.Name == waferId, ct);
    return entity is null ? null : ToRow(entity);
  }

  public async Task DeleteAsync(long id, CancellationToken ct = default) {
    await using var db = await _factory.CreateDbContextAsync(ct);
    var entity = await db.WaferInfos.FindAsync([id], ct);
    if (entity is not null) {
      db.WaferInfos.Remove(entity);
      await db.SaveChangesAsync(ct);
    }
  }

  private static WaferInfoRow ToRow(WaferInfoEntity entity) {
    var info = JsonSerializer.Deserialize<WaferInfo>(entity.Json, RepositoryJsonOptions.Default)!;
    var row = new WaferInfoRow { Id = entity.Id, Name = entity.Name, DieParametersId = entity.DieParametersId };
    row.LoadFrom(info);
    return row;
  }
}
