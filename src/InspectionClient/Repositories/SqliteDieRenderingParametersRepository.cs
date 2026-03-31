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

  public async Task SaveAsync(string name, DieRenderingParameters parameters, CancellationToken ct = default)
  {
    var json = JsonSerializer.Serialize(ParametersDto.From(parameters), RepositoryJsonOptions.Default);

    await using var cmd = _db.Connection.CreateCommand();
    cmd.CommandText = """
      INSERT INTO DieRenderingParameters (Name, Json)
      VALUES ($name, $json)
      ON CONFLICT(Name) DO UPDATE SET Json = excluded.Json
      """;
    cmd.Parameters.AddWithValue("$name", name);
    cmd.Parameters.AddWithValue("$json", json);
    await cmd.ExecuteNonQueryAsync(ct);
  }

  public async Task<DieRenderingParameters?> FindAsync(string name, CancellationToken ct = default)
  {
    await using var cmd = _db.Connection.CreateCommand();
    cmd.CommandText = "SELECT Json FROM DieRenderingParameters WHERE Name = $name";
    cmd.Parameters.AddWithValue("$name", name);

    var json = await cmd.ExecuteScalarAsync(ct) as string;
    if (json is null)
      return null;

    var dto = JsonSerializer.Deserialize<ParametersDto>(json, RepositoryJsonOptions.Default);
    return dto?.ToParameters();
  }

  public async Task<IReadOnlyList<DieParametersEntry>> ListAsync(CancellationToken ct = default)
  {
    await using var cmd = _db.Connection.CreateCommand();
    cmd.CommandText = "SELECT Name, Json FROM DieRenderingParameters ORDER BY Name ASC";

    var list = new List<DieParametersEntry>();
    await using var reader = await cmd.ExecuteReaderAsync(ct);
    while (await reader.ReadAsync(ct))
    {
      var dto = JsonSerializer.Deserialize<ParametersDto>(reader.GetString(1), RepositoryJsonOptions.Default);
      if (dto is not null)
        list.Add(new DieParametersEntry(reader.GetString(0), dto.ToParameters()));
    }

    return list;
  }

  public async Task DeleteAsync(string name, CancellationToken ct = default)
  {
    await using var cmd = _db.Connection.CreateCommand();
    cmd.CommandText = "DELETE FROM DieRenderingParameters WHERE Name = $name";
    cmd.Parameters.AddWithValue("$name", name);
    await cmd.ExecuteNonQueryAsync(ct);
  }

  // ── 직렬화 전용 DTO ───────────────────────────────────────────────────
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
