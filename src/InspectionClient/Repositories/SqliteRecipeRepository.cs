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

  public Task SaveAsync(WaferSurfaceInspectionRecipe recipe, CancellationToken ct = default)
  {
    ArgumentNullException.ThrowIfNull(recipe);

    var createdAt = DateTimeOffset.UtcNow.ToString("O");
    using var cmd = _db.Connection.CreateCommand();
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
    cmd.ExecuteNonQuery();

    return Task.CompletedTask;
  }

  public Task<WaferSurfaceInspectionRecipe?> FindAsync(string recipeName, CancellationToken ct = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(recipeName);

    using var cmd = _db.Connection.CreateCommand();
    cmd.CommandText = "SELECT Json FROM Recipe WHERE RecipeName = $recipeName";
    cmd.Parameters.AddWithValue("$recipeName", recipeName);

    var json = cmd.ExecuteScalar() as string;
    if (json is null)
      return Task.FromResult<WaferSurfaceInspectionRecipe?>(null);

    var result = JsonSerializer.Deserialize<WaferSurfaceInspectionRecipe>(json, RepositoryJsonOptions.Default);
    return Task.FromResult(result);
  }

  public Task<IReadOnlyList<WaferSurfaceInspectionRecipe>> ListAsync(CancellationToken ct = default)
  {
    using var cmd = _db.Connection.CreateCommand();
    cmd.CommandText = "SELECT Json FROM Recipe ORDER BY CreatedAt DESC";

    var list = new List<WaferSurfaceInspectionRecipe>();
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
      var item = JsonSerializer.Deserialize<WaferSurfaceInspectionRecipe>(reader.GetString(0), RepositoryJsonOptions.Default);
      if (item is not null)
        list.Add(item);
    }

    return Task.FromResult<IReadOnlyList<WaferSurfaceInspectionRecipe>>(list);
  }

  public Task DeleteAsync(string recipeName, CancellationToken ct = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(recipeName);

    using var cmd = _db.Connection.CreateCommand();
    cmd.CommandText = "DELETE FROM Recipe WHERE RecipeName = $recipeName";
    cmd.Parameters.AddWithValue("$recipeName", recipeName);
    cmd.ExecuteNonQuery();

    return Task.CompletedTask;
  }
}
