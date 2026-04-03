namespace InspectionClient.Interfaces;

/// <summary>
/// AUTOINCREMENT PK를 가진 Row 모델의 공통 계약.
/// </summary>
public interface IRowId
{
  long Id { get; }
}
