namespace InspectionRecipe.Interfaces;

/// <summary>
/// 검사 레시피의 공통 계약입니다.
/// 모든 레시피는 식별 정보와 검사 대상 웨이퍼 식별자를 포함합니다.
/// </summary>
public interface IInspectionRecipe {
  string RecipeName  { get; }
  string Description { get; }
  string WaferId     { get; }
}
