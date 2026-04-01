using Core.Models;

namespace InspectionClient.Models;

/// <summary>
/// WaferInfo 테이블의 row 모델.
/// Id: AUTOINCREMENT PK (재사용 불가). Name: UNIQUE 사용자 식별 이름.
/// Info: 실제 도메인 데이터 (WaferId는 Info 내부에 보존).
/// </summary>
public sealed record WaferInfoRow(long Id, string Name, WaferInfo Info);
