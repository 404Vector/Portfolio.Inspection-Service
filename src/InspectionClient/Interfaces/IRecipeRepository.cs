using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InspectionRecipe.Models;

namespace InspectionClient.Interfaces;

/// <summary>
/// WaferSurfaceInspectionRecipe 영속성 계약.
/// </summary>
public interface IRecipeRepository
{
  Task SaveAsync(WaferSurfaceInspectionRecipe recipe, CancellationToken ct = default);
  Task<WaferSurfaceInspectionRecipe?> FindAsync(string recipeName, CancellationToken ct = default);
  Task<IReadOnlyList<WaferSurfaceInspectionRecipe>> ListAsync(CancellationToken ct = default);
  Task DeleteAsync(string recipeName, CancellationToken ct = default);
}
