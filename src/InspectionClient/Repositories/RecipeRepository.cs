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

public sealed class RecipeRepository : IRecipeRepository {
  private readonly IDbContextFactory<InspectionDbContext> _factory;

  public RecipeRepository(IDbContextFactory<InspectionDbContext> factory) {
    _factory = factory;
  }

  public async Task<RecipeRow> CreateAsync(string name, CancellationToken ct = default) {
    ArgumentException.ThrowIfNullOrWhiteSpace(name);

    var recipe = new WaferSurfaceInspectionRecipe(
      RecipeName: name, Description: string.Empty,
      Fov: new FovSize(1413.0, 1035.0));
    var entity = new RecipeEntity {
      Name = name,
      CreatedAt = DateTimeOffset.UtcNow.ToString("O"),
      Json = JsonSerializer.Serialize(recipe, RepositoryJsonOptions.Default),
    };

    await using var db = await _factory.CreateDbContextAsync(ct);
    db.Recipes.Add(entity);
    await db.SaveChangesAsync(ct);
    return new RecipeRow { Id = entity.Id, Recipe = recipe };
  }

  public async Task<RecipeRow?> FindByIdAsync(long id, CancellationToken ct = default) {
    await using var db = await _factory.CreateDbContextAsync(ct);
    var entity = await db.Recipes.AsNoTracking()
        .FirstOrDefaultAsync(r => r.Id == id, ct);
    return entity is null ? null : ToRow(entity);
  }

  public async Task<IReadOnlyList<RecipeRow>> ListAsync(CancellationToken ct = default) {
    await using var db = await _factory.CreateDbContextAsync(ct);
    var entities = await db.Recipes.AsNoTracking()
        .OrderByDescending(r => r.CreatedAt)
        .ToListAsync(ct);
    return entities.Select(ToRow).ToList();
  }

  public async Task UpdateAsync(RecipeRow item, CancellationToken ct = default) {
    ArgumentNullException.ThrowIfNull(item);

    await using var db = await _factory.CreateDbContextAsync(ct);
    var entity = await db.Recipes.FindAsync([item.Id], ct)
        ?? throw new InvalidOperationException($"Recipe Id={item.Id} not found.");
    entity.Name = item.Recipe.RecipeName;
    entity.CreatedAt = DateTimeOffset.UtcNow.ToString("O");
    entity.Json = JsonSerializer.Serialize(item.Recipe, RepositoryJsonOptions.Default);
    await db.SaveChangesAsync(ct);
  }

  public async Task DeleteAsync(long id, CancellationToken ct = default) {
    await using var db = await _factory.CreateDbContextAsync(ct);
    var entity = await db.Recipes.FindAsync([id], ct);
    if (entity is not null) {
      db.Recipes.Remove(entity);
      await db.SaveChangesAsync(ct);
    }
  }

  private static RecipeRow ToRow(RecipeEntity entity) {
    var recipe = JsonSerializer.Deserialize<WaferSurfaceInspectionRecipe>(
        entity.Json, RepositoryJsonOptions.Default)!;
    return new RecipeRow { Id = entity.Id, Recipe = recipe };
  }
}
