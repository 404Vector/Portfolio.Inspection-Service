using InspectionRecipe.Models;

namespace InspectionClient.Models;

/// <summary>
/// Recipe 테이블의 row 모델.
/// Id: AUTOINCREMENT PK (재사용 불가). Name: UNIQUE 사용자 식별 이름.
/// Recipe: 실제 도메인 데이터.
/// </summary>
public sealed class RecipeRow
{
  public long                        Id     { get; set; }
  public string                      Name   { get; set; } = string.Empty;
  public WaferSurfaceInspectionRecipe Recipe { get; set; } = null!;
}
