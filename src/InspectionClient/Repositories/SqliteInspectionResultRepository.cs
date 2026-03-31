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

public sealed class SqliteInspectionResultRepository : IInspectionResultRepository
{
  private readonly InspectionDatabase _db;

  public SqliteInspectionResultRepository(InspectionDatabase db)
  {
    _db = db;
  }

  public Task<string> SaveAsync(WaferSurfaceInspectionResult result, CancellationToken ct = default)
  {
    ArgumentNullException.ThrowIfNull(result);

    var resultId = Guid.NewGuid().ToString();
    using var cmd = _db.Connection.CreateCommand();
    cmd.CommandText = """
      INSERT INTO InspectionResult
        (ResultId, RecipeName, WaferId, Status, StartedAt, CompletedAt, Json)
      VALUES
        ($resultId, $recipeName, $waferId, $status, $startedAt, $completedAt, $json)
      """;
    cmd.Parameters.AddWithValue("$resultId",    resultId);
    cmd.Parameters.AddWithValue("$recipeName",  result.RecipeName);
    cmd.Parameters.AddWithValue("$waferId",     result.Wafer.WaferId);
    cmd.Parameters.AddWithValue("$status",      result.Status.ToString());
    cmd.Parameters.AddWithValue("$startedAt",   result.StartedAt.ToString("O"));
    cmd.Parameters.AddWithValue("$completedAt", result.CompletedAt.ToString("O"));
    cmd.Parameters.AddWithValue("$json",        JsonSerializer.Serialize(result, RepositoryJsonOptions.Default));
    cmd.ExecuteNonQuery();

    return Task.FromResult(resultId);
  }

  public Task<WaferSurfaceInspectionResult?> FindAsync(string resultId, CancellationToken ct = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(resultId);

    using var cmd = _db.Connection.CreateCommand();
    cmd.CommandText = "SELECT Json FROM InspectionResult WHERE ResultId = $resultId";
    cmd.Parameters.AddWithValue("$resultId", resultId);

    var json = cmd.ExecuteScalar() as string;
    if (json is null)
      return Task.FromResult<WaferSurfaceInspectionResult?>(null);

    var result = JsonSerializer.Deserialize<WaferSurfaceInspectionResult>(json, RepositoryJsonOptions.Default);
    return Task.FromResult(result);
  }

  public Task<IReadOnlyList<WaferSurfaceInspectionResult>> ListAsync(CancellationToken ct = default)
  {
    using var cmd = _db.Connection.CreateCommand();
    cmd.CommandText = "SELECT Json FROM InspectionResult ORDER BY StartedAt DESC";

    var list = new List<WaferSurfaceInspectionResult>();
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
      var item = JsonSerializer.Deserialize<WaferSurfaceInspectionResult>(reader.GetString(0), RepositoryJsonOptions.Default);
      if (item is not null)
        list.Add(item);
    }

    return Task.FromResult<IReadOnlyList<WaferSurfaceInspectionResult>>(list);
  }

  public Task<IReadOnlyList<InspectionResultEntry>> ListEntriesAsync(CancellationToken ct = default)
  {
    using var cmd = _db.Connection.CreateCommand();
    cmd.CommandText = "SELECT ResultId, Json FROM InspectionResult ORDER BY StartedAt DESC";

    var list = new List<InspectionResultEntry>();
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
      var resultId = reader.GetString(0);
      var item = JsonSerializer.Deserialize<WaferSurfaceInspectionResult>(reader.GetString(1), RepositoryJsonOptions.Default);
      if (item is not null)
        list.Add(new InspectionResultEntry(resultId, item));
    }

    return Task.FromResult<IReadOnlyList<InspectionResultEntry>>(list);
  }

  public Task DeleteAsync(string resultId, CancellationToken ct = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(resultId);

    using var cmd = _db.Connection.CreateCommand();
    cmd.CommandText = "DELETE FROM InspectionResult WHERE ResultId = $resultId";
    cmd.Parameters.AddWithValue("$resultId", resultId);
    cmd.ExecuteNonQuery();

    return Task.CompletedTask;
  }
}
