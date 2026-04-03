using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Core.Models;
using InspectionClient.Interfaces;
using InspectionClient.Models;
using InspectionClient.Services;
using InspectionRecipe.Models;

namespace InspectionClient.Repositories;

public sealed class SqliteDieSpotRecipeRepository : IDieSpotRecipeRepository
{
  private readonly InspectionDatabase _db;

  public SqliteDieSpotRecipeRepository(InspectionDatabase db)
  {
    _db = db;
  }

  public async Task<DieSpotRecipeRow> CreateAsync(string name, CancellationToken ct = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(name);

    var recipe    = new DieSpotInspectionRecipe(
      RecipeName:  name,
      Description: string.Empty,
      Fov:         new FovSize(1413.0, 1035.0),
      ShotCenter:  WaferCoordinate.Origin);
    var createdAt = DateTimeOffset.UtcNow.ToString("O");
    var json      = JsonSerializer.Serialize(recipe, RepositoryJsonOptions.Default);

    await using var cmd = _db.Connection.CreateCommand();
    cmd.CommandText = """
      INSERT INTO DieSpotRecipe (Name, CreatedAt, Json)
      VALUES ($name, $createdAt, $json)
      RETURNING Id
      """;
    cmd.Parameters.AddWithValue("$name",      name);
    cmd.Parameters.AddWithValue("$createdAt", createdAt);
    cmd.Parameters.AddWithValue("$json",      json);

    var id = Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));
    return new DieSpotRecipeRow { Id = id, Recipe = recipe };
  }

  public async Task<DieSpotRecipeRow?> FindByIdAsync(long id, CancellationToken ct = default)
  {
    await using var cmd = _db.Connection.CreateCommand();
    cmd.CommandText = "SELECT Id, Json FROM DieSpotRecipe WHERE Id = $id";
    cmd.Parameters.AddWithValue("$id", id);

    await using var reader = await cmd.ExecuteReaderAsync(ct);
    if (!await reader.ReadAsync(ct))
      return null;

    return ReadRow(reader);
  }

  public async Task<IReadOnlyList<DieSpotRecipeRow>> ListAsync(CancellationToken ct = default)
  {
    await using var cmd = _db.Connection.CreateCommand();
    cmd.CommandText = "SELECT Id, Json FROM DieSpotRecipe ORDER BY CreatedAt DESC";

    var list = new List<DieSpotRecipeRow>();
    await using var reader = await cmd.ExecuteReaderAsync(ct);
    while (await reader.ReadAsync(ct))
      list.Add(ReadRow(reader));

    return list;
  }

  public async Task UpdateAsync(DieSpotRecipeRow item, CancellationToken ct = default)
  {
    ArgumentNullException.ThrowIfNull(item);

    var createdAt = DateTimeOffset.UtcNow.ToString("O");
    var json      = JsonSerializer.Serialize(item.Recipe, RepositoryJsonOptions.Default);

    await using var cmd = _db.Connection.CreateCommand();
    cmd.CommandText = """
      UPDATE DieSpotRecipe
      SET Name = $name, CreatedAt = $createdAt, Json = $json
      WHERE Id = $id
      """;
    cmd.Parameters.AddWithValue("$id",        item.Id);
    cmd.Parameters.AddWithValue("$name",      item.Recipe.RecipeName);
    cmd.Parameters.AddWithValue("$createdAt", createdAt);
    cmd.Parameters.AddWithValue("$json",      json);
    await cmd.ExecuteNonQueryAsync(ct);
  }

  public async Task DeleteAsync(long id, CancellationToken ct = default)
  {
    await using var cmd = _db.Connection.CreateCommand();
    cmd.CommandText = "DELETE FROM DieSpotRecipe WHERE Id = $id";
    cmd.Parameters.AddWithValue("$id", id);
    await cmd.ExecuteNonQueryAsync(ct);
  }

  // ── 헬퍼 ─────────────────────────────────────────────────────────────────

  private static DieSpotRecipeRow ReadRow(Microsoft.Data.Sqlite.SqliteDataReader reader)
  {
    var id     = reader.GetInt64(0);
    var recipe = JsonSerializer.Deserialize<DieSpotInspectionRecipe>(reader.GetString(1), RepositoryJsonOptions.Default);
    return new DieSpotRecipeRow { Id = id, Recipe = recipe! };
  }
}
