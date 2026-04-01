using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using InspectionClient.Interfaces;
using InspectionClient.Models;
using InspectionClient.Services;

namespace InspectionClient.Repositories;

public sealed class SqliteDieRenderingParametersRepository : IDieRenderingParametersRepository
{
  private readonly InspectionDatabase _db;

  public SqliteDieRenderingParametersRepository(InspectionDatabase db)
  {
    _db = db;
  }

  public async Task<DieParametersRow> CreateAsync(string name, CancellationToken ct = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(name);

    var defaults = new DieRenderingParameters();
    var json     = Serialize(defaults);

    await using var cmd = _db.Connection.CreateCommand();
    cmd.CommandText = """
      INSERT INTO DieRenderingParameters (Name, Json)
      VALUES ($name, $json)
      RETURNING Id
      """;
    cmd.Parameters.AddWithValue("$name", name);
    cmd.Parameters.AddWithValue("$json", json);

    var id = Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));
    return new DieParametersRow { Id = id, Name = name, Parameters = defaults };
  }

  public async Task<DieParametersRow?> FindByIdAsync(long id, CancellationToken ct = default)
  {
    await using var cmd = _db.Connection.CreateCommand();
    cmd.CommandText = "SELECT Id, Name, Json FROM DieRenderingParameters WHERE Id = $id";
    cmd.Parameters.AddWithValue("$id", id);

    await using var reader = await cmd.ExecuteReaderAsync(ct);
    if (!await reader.ReadAsync(ct))
      return null;

    return ReadRow(reader);
  }

  public async Task<IReadOnlyList<DieParametersRow>> ListAsync(CancellationToken ct = default)
  {
    await using var cmd = _db.Connection.CreateCommand();
    cmd.CommandText = "SELECT Id, Name, Json FROM DieRenderingParameters ORDER BY Name ASC";

    var list = new List<DieParametersRow>();
    await using var reader = await cmd.ExecuteReaderAsync(ct);
    while (await reader.ReadAsync(ct))
      list.Add(ReadRow(reader));

    return list;
  }

  public async Task UpdateAsync(DieParametersRow item, CancellationToken ct = default)
  {
    ArgumentNullException.ThrowIfNull(item);

    var json = Serialize(item.Parameters);

    await using var cmd = _db.Connection.CreateCommand();
    cmd.CommandText = """
      UPDATE DieRenderingParameters
      SET Name = $name, Json = $json
      WHERE Id = $id
      """;
    cmd.Parameters.AddWithValue("$id",   item.Id);
    cmd.Parameters.AddWithValue("$name", item.Name);
    cmd.Parameters.AddWithValue("$json", json);
    await cmd.ExecuteNonQueryAsync(ct);
  }

  public async Task DeleteAsync(long id, CancellationToken ct = default)
  {
    await using var cmd = _db.Connection.CreateCommand();
    cmd.CommandText = "DELETE FROM DieRenderingParameters WHERE Id = $id";
    cmd.Parameters.AddWithValue("$id", id);
    await cmd.ExecuteNonQueryAsync(ct);
  }

  // ── 헬퍼 ─────────────────────────────────────────────────────────────────

  private static DieParametersRow ReadRow(Microsoft.Data.Sqlite.SqliteDataReader reader)
  {
    var id   = reader.GetInt64(0);
    var name = reader.GetString(1);
    var dto  = JsonSerializer.Deserialize<ParametersDto>(reader.GetString(2), RepositoryJsonOptions.Default);
    return new DieParametersRow { Id = id, Name = name, Parameters = dto?.ToParameters() ?? new DieRenderingParameters() };
  }

  private static string Serialize(DieRenderingParameters p) =>
      JsonSerializer.Serialize(ParametersDto.From(p), RepositoryJsonOptions.Default);

  // ── 직렬화 전용 DTO ───────────────────────────────────────────────────────
  // DieRenderingParameters는 ObservableObject이므로 직접 직렬화하지 않고
  // 순수 데이터 DTO를 통해 직렬화한다.

  private sealed record ParametersDto(
    [property: JsonPropertyName("canvasWidth")]         int  CanvasWidth,
    [property: JsonPropertyName("canvasHeight")]        int  CanvasHeight,
    [property: JsonPropertyName("backgroundGray")]      byte BackgroundGray,
    [property: JsonPropertyName("showAlignmentMarks")]  bool ShowAlignmentMarks,
    [property: JsonPropertyName("showRuler")]           bool ShowRuler,
    [property: JsonPropertyName("showTextureBands")]    bool ShowTextureBands,
    [property: JsonPropertyName("showCalibrationMark")] bool ShowCalibrationMark,
    [property: JsonPropertyName("padRowCount")]         int  PadRowCount,
    [property: JsonPropertyName("padColumnCount")]      int  PadColumnCount)
  {
    public static ParametersDto From(DieRenderingParameters p) => new(
      p.CanvasWidth,
      p.CanvasHeight,
      p.BackgroundGray,
      p.ShowAlignmentMarks,
      p.ShowRuler,
      p.ShowTextureBands,
      p.ShowCalibrationMark,
      p.PadRowCount,
      p.PadColumnCount);

    public DieRenderingParameters ToParameters() => new()
    {
      CanvasWidth         = CanvasWidth,
      CanvasHeight        = CanvasHeight,
      BackgroundGray      = BackgroundGray,
      ShowAlignmentMarks  = ShowAlignmentMarks,
      ShowRuler           = ShowRuler,
      ShowTextureBands    = ShowTextureBands,
      ShowCalibrationMark = ShowCalibrationMark,
      PadRowCount         = PadRowCount,
      PadColumnCount      = PadColumnCount,
    };
  }
}
