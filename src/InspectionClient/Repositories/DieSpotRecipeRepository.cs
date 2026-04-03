using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Core.Models;
using InspectionClient.Infrastructure;
using InspectionClient.Infrastructure.Entities;
using InspectionClient.Interfaces;
using InspectionClient.Models;
using InspectionRecipe.Models;
using Microsoft.EntityFrameworkCore;

namespace InspectionClient.Repositories;

public sealed class DieSpotRecipeRepository : IDieSpotRecipeRepository {
  private readonly InspectionDbContext _db;

  public DieSpotRecipeRepository(InspectionDbContext db) {
    _db = db;
  }

  public async Task<DieSpotRecipeRow> CreateAsync(string name, CancellationToken ct = default) {
    ArgumentException.ThrowIfNullOrWhiteSpace(name);

    var recipe = new DieSpotInspectionRecipe(
      RecipeName: name, Description: string.Empty,
      Fov: new FovSize(1413.0, 1035.0),
      ShotCenter: WaferCoordinate.Origin);
    var entity = new DieSpotRecipeEntity {
      Name = name,
      CreatedAt = DateTimeOffset.UtcNow.ToString("O"),
      Json = JsonSerializer.Serialize(recipe, RepositoryJsonOptions.Default),
    };

    _db.DieSpotRecipes.Add(entity);
    await _db.SaveChangesAsync(ct);
    return new DieSpotRecipeRow { Id = entity.Id, Recipe = recipe };
  }

  public async Task<DieSpotRecipeRow?> FindByIdAsync(long id, CancellationToken ct = default) {
    var entity = await _db.DieSpotRecipes.AsNoTracking()
        .FirstOrDefaultAsync(r => r.Id == id, ct);
    return entity is null ? null : ToRow(entity);
  }

  public async Task<IReadOnlyList<DieSpotRecipeRow>> ListAsync(CancellationToken ct = default) {
    var entities = await _db.DieSpotRecipes.AsNoTracking()
        .OrderByDescending(r => r.CreatedAt)
        .ToListAsync(ct);
    return entities.Select(ToRow).ToList();
  }

  public async Task UpdateAsync(DieSpotRecipeRow item, CancellationToken ct = default) {
    ArgumentNullException.ThrowIfNull(item);

    var entity = await _db.DieSpotRecipes.FindAsync([item.Id], ct)
        ?? throw new InvalidOperationException($"DieSpotRecipe Id={item.Id} not found.");
    entity.Name = item.Recipe.RecipeName;
    entity.CreatedAt = DateTimeOffset.UtcNow.ToString("O");
    entity.Json = JsonSerializer.Serialize(item.Recipe, RepositoryJsonOptions.Default);
    await _db.SaveChangesAsync(ct);
  }

  public async Task DeleteAsync(long id, CancellationToken ct = default) {
    var entity = await _db.DieSpotRecipes.FindAsync([id], ct);
    if (entity is not null) {
      _db.DieSpotRecipes.Remove(entity);
      await _db.SaveChangesAsync(ct);
    }
  }

  private static DieSpotRecipeRow ToRow(DieSpotRecipeEntity entity) {
    var recipe = JsonSerializer.Deserialize<DieSpotInspectionRecipe>(
        entity.Json, RepositoryJsonOptions.Default)!;
    return new DieSpotRecipeRow { Id = entity.Id, Recipe = recipe };
  }
}
