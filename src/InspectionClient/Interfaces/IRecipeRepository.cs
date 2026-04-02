using InspectionClient.Models;

namespace InspectionClient.Interfaces;

/// <summary>
/// Recipe 테이블 CRUD 계약.
/// </summary>
public interface IRecipeRepository : INamedRepository<RecipeRow>
{
}
