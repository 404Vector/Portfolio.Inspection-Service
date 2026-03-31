using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using InspectionClient.Interfaces;
using InspectionClient.Services;
using InspectionRecipe.Models;
using Microsoft.Data.Sqlite;

namespace InspectionClient.Repositories;

public sealed class SqliteRecipeRepository : IRecipeRepository
{
  private readonly InspectionDatabase _db;

  public SqliteRecipeRepository(InspectionDatabase db)
  {
    _db = db;
  }

  public async Task SaveAsync(WaferSurfaceInspectionRecipe recipe, CancellationToken ct = default)
  {
    ArgumentNullException.ThrowIfNull(recipe);

    var createdAt = DateTimeOffset.UtcNow.ToString("O");
    await using var cmd = _db.Connection.CreateCommand();
    cmd.CommandText = """
      INSERT INTO Recipe (RecipeName, WaferId, CreatedAt, Json)
      VALUES ($recipeName, $waferId, $createdAt, $json)
      ON CONFLICT(RecipeName) DO UPDATE SET
        WaferId   = excluded.WaferId,
        CreatedAt = excluded.CreatedAt,
        Json      = excluded.Json
      """;
    cmd.Parameters.AddWithValue("$recipeName", recipe.RecipeName);
    cmd.Parameters.AddWithValue("$waferId",    recipe.Wafer.WaferId);
    cmd.Parameters.AddWithValue("$createdAt",  createdAt);
    cmd.Parameters.AddWithValue("$json",       JsonSerializer.Serialize(recipe, RepositoryJsonOptions.Default));
    await cmd.ExecuteNonQueryAsync(ct);
  }

  public async Task<WaferSurfaceInspectionRecipe?> FindAsync(string recipeName, CancellationToken ct = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(recipeName);

    await using var cmd = _db.Connection.CreateCommand();
    cmd.CommandText = "SELECT Json FROM Recipe WHERE RecipeName = $recipeName";
    cmd.Parameters.AddWithValue("$recipeName", recipeName);

    var json = await cmd.ExecuteScalarAsync(ct) as string;
    if (json is null)
      return null;

    return JsonSerializer.Deserialize<WaferSurfaceInspectionRecipe>(json, RepositoryJsonOptions.Default);
  }

  public async Task<IReadOnlyList<WaferSurfaceInspectionRecipe>> ListAsync(CancellationToken ct = default)
  {
    await using var cmd = _db.Connection.CreateCommand();
    cmd.CommandText = "SELECT Json FROM Recipe ORDER BY CreatedAt DESC";

    var list = new List<WaferSurfaceInspectionRecipe>();
    await using var reader = await cmd.ExecuteReaderAsync(ct);
    while (await reader.ReadAsync(ct))
    {
      var item = JsonSerializer.Deserialize<WaferSurfaceInspectionRecipe>(reader.GetString(0), RepositoryJsonOptions.Default);
      if (item is not null)
        list.Add(item);
    }

    return list;
  }

  public async Task DeleteAsync(string recipeName, CancellationToken ct = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(recipeName);

    await using var cmd = _db.Connection.CreateCommand();
    cmd.CommandText = "DELETE FROM Recipe WHERE RecipeName = $recipeName";
    cmd.Parameters.AddWithValue("$recipeName", recipeName);
    await cmd.ExecuteNonQueryAsync(ct);
  }
}
