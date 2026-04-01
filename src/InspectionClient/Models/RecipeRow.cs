using InspectionRecipe.Models;

namespace InspectionClient.Models;

/// <summary>
/// Recipe 테이블의 row 모델.
/// Id: AUTOINCREMENT PK (재사용 불가). Name: UNIQUE 사용자 식별 이름.
/// Recipe: 실제 도메인 데이터 (RecipeName은 Recipe 내부에 보존).
/// </summary>
public sealed record RecipeRow(long Id, string Name, WaferSurfaceInspectionRecipe Recipe);
