using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using InspectionClient.Infrastructure;
using InspectionClient.Infrastructure.Entities;
using InspectionClient.Interfaces;
using InspectionClient.Models;
using Microsoft.EntityFrameworkCore;

namespace InspectionClient.Repositories;

public sealed class DieRenderingParametersRepository : IDieRenderingParametersRepository {
  private readonly InspectionDbContext _db;

  public DieRenderingParametersRepository(InspectionDbContext db) {
    _db = db;
  }

  public async Task<DieParametersRow> CreateAsync(string name, CancellationToken ct = default) {
    ArgumentException.ThrowIfNullOrWhiteSpace(name);

    var defaults = new DieRenderingParameters();
    var entity = new DieRenderingParametersEntity {
      Name = name,
      Json = Serialize(defaults),
    };

    _db.DieRenderingParameters.Add(entity);
    await _db.SaveChangesAsync(ct);
    return new DieParametersRow { Id = entity.Id, Name = name, Parameters = defaults };
  }

  public async Task<DieParametersRow?> FindByIdAsync(long id, CancellationToken ct = default) {
    var entity = await _db.DieRenderingParameters.AsNoTracking()
        .FirstOrDefaultAsync(r => r.Id == id, ct);
    return entity is null ? null : ToRow(entity);
  }

  public async Task<IReadOnlyList<DieParametersRow>> ListAsync(CancellationToken ct = default) {
    var entities = await _db.DieRenderingParameters.AsNoTracking()
        .OrderByDescending(r => r.Id)
        .ToListAsync(ct);
    return entities.Select(ToRow).ToList();
  }

  public async Task UpdateAsync(DieParametersRow item, CancellationToken ct = default) {
    ArgumentNullException.ThrowIfNull(item);

    var entity = await _db.DieRenderingParameters.FindAsync([item.Id], ct)
        ?? throw new InvalidOperationException($"DieRenderingParameters Id={item.Id} not found.");
    entity.Name = item.Name;
    entity.Json = Serialize(item.Parameters);
    await _db.SaveChangesAsync(ct);
  }

  public async Task DeleteAsync(long id, CancellationToken ct = default) {
    var entity = await _db.DieRenderingParameters.FindAsync([id], ct);
    if (entity is not null) {
      _db.DieRenderingParameters.Remove(entity);
      await _db.SaveChangesAsync(ct);
    }
  }

  // ── 헬퍼 ─────────────────────────────────────────────────────────────────

  private static DieParametersRow ToRow(DieRenderingParametersEntity entity) {
    var dto = JsonSerializer.Deserialize<ParametersDto>(entity.Json, RepositoryJsonOptions.Default);
    return new DieParametersRow {
      Id = entity.Id, Name = entity.Name,
      Parameters = dto?.ToParameters() ?? new DieRenderingParameters(),
    };
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
    [property: JsonPropertyName("padColumnCount")]      int  PadColumnCount) {
    public static ParametersDto From(DieRenderingParameters p) => new(
      p.CanvasWidth, p.CanvasHeight, p.BackgroundGray,
      p.ShowAlignmentMarks, p.ShowRuler, p.ShowTextureBands,
      p.ShowCalibrationMark, p.PadRowCount, p.PadColumnCount);

    public DieRenderingParameters ToParameters() => new() {
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
