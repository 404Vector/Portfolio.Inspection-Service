using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Core.Models;
using InspectionClient.Interfaces;
using InspectionClient.Models;
using InspectionClient.Services;

namespace InspectionClient.Repositories;

public sealed class SqliteWaferInfoRepository : IWaferInfoRepository
{
  private readonly InspectionDatabase _db;

  public SqliteWaferInfoRepository(InspectionDatabase db)
  {
    _db = db;
  }

  public async Task<WaferInfoRow> CreateAsync(string name, CancellationToken ct = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(name);

    var info = WaferInfo.CreateDummy() with { WaferId = name };
    var json = JsonSerializer.Serialize(info, RepositoryJsonOptions.Default);

    await using var cmd = _db.Connection.CreateCommand();
    cmd.CommandText = """
      INSERT INTO WaferInfo (Name, WaferType, CreatedAt, Json)
      VALUES ($name, $waferType, $createdAt, $json)
      RETURNING Id
      """;
    cmd.Parameters.AddWithValue("$name",      name);
    cmd.Parameters.AddWithValue("$waferType", info.WaferType.ToString());
    cmd.Parameters.AddWithValue("$createdAt", info.CreatedAt.ToString("O"));
    cmd.Parameters.AddWithValue("$json",      json);

    var id = Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));
    return new WaferInfoRow(id, name, info);
  }

  public async Task<WaferInfoRow?> FindByIdAsync(long id, CancellationToken ct = default)
  {
    await using var cmd = _db.Connection.CreateCommand();
    cmd.CommandText = "SELECT Id, Name, Json FROM WaferInfo WHERE Id = $id";
    cmd.Parameters.AddWithValue("$id", id);

    await using var reader = await cmd.ExecuteReaderAsync(ct);
    if (!await reader.ReadAsync(ct))
      return null;

    return ReadRow(reader);
  }

  public async Task<IReadOnlyList<WaferInfoRow>> ListAsync(CancellationToken ct = default)
  {
    await using var cmd = _db.Connection.CreateCommand();
    cmd.CommandText = "SELECT Id, Name, Json FROM WaferInfo ORDER BY CreatedAt DESC";

    var list = new List<WaferInfoRow>();
    await using var reader = await cmd.ExecuteReaderAsync(ct);
    while (await reader.ReadAsync(ct))
      list.Add(ReadRow(reader));

    return list;
  }

  public async Task UpdateAsync(WaferInfoRow item, CancellationToken ct = default)
  {
    ArgumentNullException.ThrowIfNull(item);

    var json = JsonSerializer.Serialize(item.Info, RepositoryJsonOptions.Default);

    await using var cmd = _db.Connection.CreateCommand();
    cmd.CommandText = """
      UPDATE WaferInfo
      SET Name = $name, WaferType = $waferType, CreatedAt = $createdAt, Json = $json
      WHERE Id = $id
      """;
    cmd.Parameters.AddWithValue("$id",        item.Id);
    cmd.Parameters.AddWithValue("$name",      item.Name);
    cmd.Parameters.AddWithValue("$waferType", item.Info.WaferType.ToString());
    cmd.Parameters.AddWithValue("$createdAt", item.Info.CreatedAt.ToString("O"));
    cmd.Parameters.AddWithValue("$json",      json);
    await cmd.ExecuteNonQueryAsync(ct);
  }

  public async Task DeleteAsync(long id, CancellationToken ct = default)
  {
    await using var cmd = _db.Connection.CreateCommand();
    cmd.CommandText = "DELETE FROM WaferInfo WHERE Id = $id";
    cmd.Parameters.AddWithValue("$id", id);
    await cmd.ExecuteNonQueryAsync(ct);
  }

  // ── 헬퍼 ─────────────────────────────────────────────────────────────────

  private static WaferInfoRow ReadRow(Microsoft.Data.Sqlite.SqliteDataReader reader)
  {
    var id   = reader.GetInt64(0);
    var name = reader.GetString(1);
    var info = JsonSerializer.Deserialize<WaferInfo>(reader.GetString(2), RepositoryJsonOptions.Default);
    return new WaferInfoRow(id, name, info!);
  }
}
