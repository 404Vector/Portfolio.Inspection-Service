using InspectionClient.Interfaces;
using InspectionRecipe.Models;

namespace InspectionClient.Models;

/// <summary>
/// Recipe 테이블의 row 모델.
/// Id: AUTOINCREMENT PK (재사용 불가).
/// Recipe.RecipeName이 UNIQUE 사용자 식별 이름을 겸한다.
/// </summary>
public sealed class RecipeRow : IRowId
{
  public long                         Id     { get; set; }
  public WaferSurfaceInspectionRecipe Recipe { get; set; } = null!;
}
