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
using Core.Recipe.Models;
using Microsoft.EntityFrameworkCore;

namespace InspectionClient.Repositories;

public sealed class DieSpotRecipeRepository : IDieSpotRecipeRepository {
  private readonly IDbContextFactory<InspectionDbContext> _factory;

  public DieSpotRecipeRepository(IDbContextFactory<InspectionDbContext> factory) {
    _factory = factory;
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

    await using var db = await _factory.CreateDbContextAsync(ct);
    db.DieSpotRecipes.Add(entity);
    await db.SaveChangesAsync(ct);
    return new DieSpotRecipeRow { Id = entity.Id, Recipe = recipe };
  }

  public async Task<DieSpotRecipeRow?> FindByIdAsync(long id, CancellationToken ct = default) {
    await using var db = await _factory.CreateDbContextAsync(ct);
    var entity = await db.DieSpotRecipes.AsNoTracking()
        .FirstOrDefaultAsync(r => r.Id == id, ct);
    return entity is null ? null : ToRow(entity);
  }

  public async Task<IReadOnlyList<DieSpotRecipeRow>> ListAsync(CancellationToken ct = default) {
    await using var db = await _factory.CreateDbContextAsync(ct);
    var entities = await db.DieSpotRecipes.AsNoTracking()
        .OrderByDescending(r => r.CreatedAt)
        .ToListAsync(ct);
    return entities.Select(ToRow).ToList();
  }

  public async Task UpdateAsync(DieSpotRecipeRow item, CancellationToken ct = default) {
    ArgumentNullException.ThrowIfNull(item);

    await using var db = await _factory.CreateDbContextAsync(ct);
    var entity = await db.DieSpotRecipes.FindAsync([item.Id], ct)
        ?? throw new InvalidOperationException($"DieSpotRecipe Id={item.Id} not found.");
    entity.Name = item.Recipe.RecipeName;
    entity.CreatedAt = DateTimeOffset.UtcNow.ToString("O");
    entity.Json = JsonSerializer.Serialize(item.Recipe, RepositoryJsonOptions.Default);
    await db.SaveChangesAsync(ct);
  }

  public async Task DeleteAsync(long id, CancellationToken ct = default) {
    await using var db = await _factory.CreateDbContextAsync(ct);
    var entity = await db.DieSpotRecipes.FindAsync([id], ct);
    if (entity is not null) {
      db.DieSpotRecipes.Remove(entity);
      await db.SaveChangesAsync(ct);
    }
  }

  private static DieSpotRecipeRow ToRow(DieSpotRecipeEntity entity) {
    var recipe = JsonSerializer.Deserialize<DieSpotInspectionRecipe>(
        entity.Json, RepositoryJsonOptions.Default)!;
    return new DieSpotRecipeRow { Id = entity.Id, Recipe = recipe };
  }
}
