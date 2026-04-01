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

public sealed class SqliteRecipeRepository : IRecipeRepository
{
  private readonly InspectionDatabase _db;

  public SqliteRecipeRepository(InspectionDatabase db)
  {
    _db = db;
  }

  public async Task<RecipeRow> CreateAsync(string name, CancellationToken ct = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(name);

    var recipe    = new WaferSurfaceInspectionRecipe(
      RecipeName: name,
      Description: string.Empty,
      Wafer: WaferInfo.CreateDummy(),
      Fov: new FovSize(1413.0, 1035.0));
    var createdAt = DateTimeOffset.UtcNow.ToString("O");
    var json      = JsonSerializer.Serialize(recipe, RepositoryJsonOptions.Default);

    await using var cmd = _db.Connection.CreateCommand();
    cmd.CommandText = """
      INSERT INTO Recipe (Name, WaferId, CreatedAt, Json)
      VALUES ($name, $waferId, $createdAt, $json)
      RETURNING Id
      """;
    cmd.Parameters.AddWithValue("$name",      name);
    cmd.Parameters.AddWithValue("$waferId",   recipe.Wafer.WaferId);
    cmd.Parameters.AddWithValue("$createdAt", createdAt);
    cmd.Parameters.AddWithValue("$json",      json);

    var id = Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));
    return new RecipeRow(id, name, recipe);
  }

  public async Task<RecipeRow?> FindByIdAsync(long id, CancellationToken ct = default)
  {
    await using var cmd = _db.Connection.CreateCommand();
    cmd.CommandText = "SELECT Id, Name, Json FROM Recipe WHERE Id = $id";
    cmd.Parameters.AddWithValue("$id", id);

    await using var reader = await cmd.ExecuteReaderAsync(ct);
    if (!await reader.ReadAsync(ct))
      return null;

    return ReadRow(reader);
  }

  public async Task<IReadOnlyList<RecipeRow>> ListAsync(CancellationToken ct = default)
  {
    await using var cmd = _db.Connection.CreateCommand();
    cmd.CommandText = "SELECT Id, Name, Json FROM Recipe ORDER BY CreatedAt DESC";

    var list = new List<RecipeRow>();
    await using var reader = await cmd.ExecuteReaderAsync(ct);
    while (await reader.ReadAsync(ct))
      list.Add(ReadRow(reader));

    return list;
  }

  public async Task UpdateAsync(RecipeRow item, CancellationToken ct = default)
  {
    ArgumentNullException.ThrowIfNull(item);

    var createdAt = DateTimeOffset.UtcNow.ToString("O");
    var json      = JsonSerializer.Serialize(item.Recipe, RepositoryJsonOptions.Default);

    await using var cmd = _db.Connection.CreateCommand();
    cmd.CommandText = """
      UPDATE Recipe
      SET Name = $name, WaferId = $waferId, CreatedAt = $createdAt, Json = $json
      WHERE Id = $id
      """;
    cmd.Parameters.AddWithValue("$id",        item.Id);
    cmd.Parameters.AddWithValue("$name",      item.Name);
    cmd.Parameters.AddWithValue("$waferId",   item.Recipe.Wafer.WaferId);
    cmd.Parameters.AddWithValue("$createdAt", createdAt);
    cmd.Parameters.AddWithValue("$json",      json);
    await cmd.ExecuteNonQueryAsync(ct);
  }

  public async Task DeleteAsync(long id, CancellationToken ct = default)
  {
    await using var cmd = _db.Connection.CreateCommand();
    cmd.CommandText = "DELETE FROM Recipe WHERE Id = $id";
    cmd.Parameters.AddWithValue("$id", id);
    await cmd.ExecuteNonQueryAsync(ct);
  }

  // ── 헬퍼 ─────────────────────────────────────────────────────────────────

  private static RecipeRow ReadRow(Microsoft.Data.Sqlite.SqliteDataReader reader)
  {
    var id     = reader.GetInt64(0);
    var name   = reader.GetString(1);
    var recipe = JsonSerializer.Deserialize<WaferSurfaceInspectionRecipe>(reader.GetString(2), RepositoryJsonOptions.Default);
    return new RecipeRow(id, name, recipe!);
  }
}
