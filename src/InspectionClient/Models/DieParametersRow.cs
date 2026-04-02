namespace InspectionClient.Models;

/// <summary>
/// DieRenderingParameters 테이블의 row 모델.
/// Id: AUTOINCREMENT PK (재사용 불가). Name: UNIQUE 사용자 식별 이름.
/// </summary>
public sealed class DieParametersRow
{
  public long                  Id         { get; set; }
  public string                Name       { get; set; } = string.Empty;
  public DieRenderingParameters Parameters { get; set; } = new();
}
