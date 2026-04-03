namespace InspectionRecipe.Interfaces;

/// <summary>
/// 검사 레시피의 공통 계약입니다.
/// 모든 레시피는 식별 정보와 검사 파라미터를 포함합니다.
/// 검사 대상 웨이퍼는 실행 시점에 별도로 선택됩니다.
/// </summary>
public interface IInspectionRecipe {
  string RecipeName  { get; }
  string Description { get; }
}
