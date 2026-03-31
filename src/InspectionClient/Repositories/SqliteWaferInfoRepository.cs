using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Core.Models;
using InspectionClient.Interfaces;
using InspectionClient.Services;
using Microsoft.Data.Sqlite;

namespace InspectionClient.Repositories;

public sealed class SqliteWaferInfoRepository : IWaferInfoRepository
{
  private readonly InspectionDatabase _db;

  public SqliteWaferInfoRepository(InspectionDatabase db)
  {
    _db = db;
  }

  public async Task SaveAsync(WaferInfo waferInfo, CancellationToken ct = default)
  {
    ArgumentNullException.ThrowIfNull(waferInfo);

    await using var cmd = _db.Connection.CreateCommand();
    cmd.CommandText = """
      INSERT INTO WaferInfo (WaferId, LotId, WaferType, CreatedAt, Json)
      VALUES ($waferId, $lotId, $waferType, $createdAt, $json)
      ON CONFLICT(WaferId) DO UPDATE SET
        LotId     = excluded.LotId,
        WaferType = excluded.WaferType,
        CreatedAt = excluded.CreatedAt,
        Json      = excluded.Json
      """;
    cmd.Parameters.AddWithValue("$waferId",   waferInfo.WaferId);
    cmd.Parameters.AddWithValue("$lotId",     waferInfo.LotId);
    cmd.Parameters.AddWithValue("$waferType", waferInfo.WaferType.ToString());
    cmd.Parameters.AddWithValue("$createdAt", waferInfo.CreatedAt.ToString("O"));
    cmd.Parameters.AddWithValue("$json",      JsonSerializer.Serialize(waferInfo, RepositoryJsonOptions.Default));
    await cmd.ExecuteNonQueryAsync(ct);
  }

  public async Task<WaferInfo?> FindAsync(string waferId, CancellationToken ct = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(waferId);

    await using var cmd = _db.Connection.CreateCommand();
    cmd.CommandText = "SELECT Json FROM WaferInfo WHERE WaferId = $waferId";
    cmd.Parameters.AddWithValue("$waferId", waferId);

    var json = await cmd.ExecuteScalarAsync(ct) as string;
    if (json is null)
      return null;

    return JsonSerializer.Deserialize<WaferInfo>(json, RepositoryJsonOptions.Default);
  }

  public async Task<IReadOnlyList<WaferInfo>> ListAsync(CancellationToken ct = default)
  {
    await using var cmd = _db.Connection.CreateCommand();
    cmd.CommandText = "SELECT Json FROM WaferInfo ORDER BY CreatedAt DESC";

    var list = new List<WaferInfo>();
    await using var reader = await cmd.ExecuteReaderAsync(ct);
    while (await reader.ReadAsync(ct))
    {
      var item = JsonSerializer.Deserialize<WaferInfo>(reader.GetString(0), RepositoryJsonOptions.Default);
      if (item is not null)
        list.Add(item);
    }

    return list;
  }

  public async Task DeleteAsync(string waferId, CancellationToken ct = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(waferId);

    await using var cmd = _db.Connection.CreateCommand();
    cmd.CommandText = "DELETE FROM WaferInfo WHERE WaferId = $waferId";
    cmd.Parameters.AddWithValue("$waferId", waferId);
    await cmd.ExecuteNonQueryAsync(ct);
  }
}
