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
  private readonly InspectionDbContext _db;

  public WaferInfoRepository(InspectionDbContext db) {
    _db = db;
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

    _db.WaferInfos.Add(entity);
    await _db.SaveChangesAsync(ct);

    var row = new WaferInfoRow { Id = entity.Id, Name = name };
    row.LoadFrom(info);
    return row;
  }

  public async Task<WaferInfoRow?> FindByIdAsync(long id, CancellationToken ct = default) {
    var entity = await _db.WaferInfos.AsNoTracking()
        .FirstOrDefaultAsync(r => r.Id == id, ct);
    return entity is null ? null : ToRow(entity);
  }

  public async Task<IReadOnlyList<WaferInfoRow>> ListAsync(CancellationToken ct = default) {
    var entities = await _db.WaferInfos.AsNoTracking()
        .OrderByDescending(r => r.Id)
        .ToListAsync(ct);
    return entities.Select(ToRow).ToList();
  }

  public async Task UpdateAsync(WaferInfoRow item, CancellationToken ct = default) {
    ArgumentNullException.ThrowIfNull(item);

    var entity = await _db.WaferInfos.FindAsync([item.Id], ct)
        ?? throw new InvalidOperationException($"WaferInfo Id={item.Id} not found.");
    var info = item.ToWaferInfo();
    entity.Name = item.Name;
    entity.WaferType = info.WaferType.ToString();
    entity.CreatedAt = info.CreatedAt.ToString("O");
    entity.DieParametersId = item.DieParametersId;
    entity.Json = JsonSerializer.Serialize(info, RepositoryJsonOptions.Default);
    await _db.SaveChangesAsync(ct);
  }

  public async Task<WaferInfoRow?> FindByWaferIdAsync(string waferId, CancellationToken ct = default) {
    ArgumentException.ThrowIfNullOrWhiteSpace(waferId);

    // JSON 내부의 WaferId를 기준으로 검색한다.
    // EF Core SQLite에서는 json_extract를 직접 사용할 수 없으므로 클라이언트 평가한다.
    var entities = await _db.WaferInfos.AsNoTracking().ToListAsync(ct);
    var entity = entities.FirstOrDefault(e => {
      var info = JsonSerializer.Deserialize<WaferInfo>(e.Json, RepositoryJsonOptions.Default);
      return info?.WaferId == waferId;
    });
    return entity is null ? null : ToRow(entity);
  }

  public async Task DeleteAsync(long id, CancellationToken ct = default) {
    var entity = await _db.WaferInfos.FindAsync([id], ct);
    if (entity is not null) {
      _db.WaferInfos.Remove(entity);
      await _db.SaveChangesAsync(ct);
    }
  }

  private static WaferInfoRow ToRow(WaferInfoEntity entity) {
    var info = JsonSerializer.Deserialize<WaferInfo>(entity.Json, RepositoryJsonOptions.Default)!;
    var row = new WaferInfoRow { Id = entity.Id, Name = entity.Name, DieParametersId = entity.DieParametersId };
    row.LoadFrom(info);
    return row;
  }
}
