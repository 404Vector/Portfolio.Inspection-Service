namespace InspectionClient.Models;

/// <summary>
/// DieRenderingParameters 테이블의 row 모델.
/// Id: AUTOINCREMENT PK (재사용 불가). Name: UNIQUE 사용자 식별 이름.
/// </summary>
public sealed record DieParametersRow(long Id, string Name, DieRenderingParameters Parameters);
