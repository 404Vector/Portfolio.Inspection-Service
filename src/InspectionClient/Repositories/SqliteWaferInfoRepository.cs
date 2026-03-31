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

  public Task SaveAsync(WaferInfo waferInfo, CancellationToken ct = default)
  {
    ArgumentNullException.ThrowIfNull(waferInfo);

    using var cmd = _db.Connection.CreateCommand();
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
    cmd.ExecuteNonQuery();

    return Task.CompletedTask;
  }

  public Task<WaferInfo?> FindAsync(string waferId, CancellationToken ct = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(waferId);

    using var cmd = _db.Connection.CreateCommand();
    cmd.CommandText = "SELECT Json FROM WaferInfo WHERE WaferId = $waferId";
    cmd.Parameters.AddWithValue("$waferId", waferId);

    var json = cmd.ExecuteScalar() as string;
    if (json is null)
      return Task.FromResult<WaferInfo?>(null);

    var result = JsonSerializer.Deserialize<WaferInfo>(json, RepositoryJsonOptions.Default);
    return Task.FromResult(result);
  }

  public Task<IReadOnlyList<WaferInfo>> ListAsync(CancellationToken ct = default)
  {
    using var cmd = _db.Connection.CreateCommand();
    cmd.CommandText = "SELECT Json FROM WaferInfo ORDER BY CreatedAt DESC";

    var list = new List<WaferInfo>();
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
      var item = JsonSerializer.Deserialize<WaferInfo>(reader.GetString(0), RepositoryJsonOptions.Default);
      if (item is not null)
        list.Add(item);
    }

    return Task.FromResult<IReadOnlyList<WaferInfo>>(list);
  }

  public Task DeleteAsync(string waferId, CancellationToken ct = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(waferId);

    using var cmd = _db.Connection.CreateCommand();
    cmd.CommandText = "DELETE FROM WaferInfo WHERE WaferId = $waferId";
    cmd.Parameters.AddWithValue("$waferId", waferId);
    cmd.ExecuteNonQuery();

    return Task.CompletedTask;
  }
}
