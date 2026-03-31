namespace Core.Models;

/// <summary>
/// 특정 엔티티(WaferInfo, Recipe, InspectionResult)에 대한 운영자 주석.
/// 엔티티 자체의 도메인 데이터와 분리된 메타데이터 계층이다.
/// </summary>
/// <param name="EntityId">
/// 주석 대상 엔티티의 식별자.
/// WaferInfo의 경우 WaferId, Recipe의 경우 RecipeName, InspectionResult의 경우 ResultId.
/// </param>
/// <param name="EntityKind">주석 대상 엔티티 종류.</param>
/// <param name="Operator">작업자 이름 또는 ID.</param>
/// <param name="Comment">자유 형식 주석 텍스트.</param>
/// <param name="Tags">분류 태그 목록.</param>
/// <param name="CreatedAt">주석 생성 시각.</param>
public record UserAnnotation(
  string                EntityId,
  EntityKind            EntityKind,
  string                Operator,
  string                Comment,
  IReadOnlyList<string> Tags,
  DateTimeOffset        CreatedAt
);
