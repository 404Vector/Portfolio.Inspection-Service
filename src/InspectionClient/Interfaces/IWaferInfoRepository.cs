using InspectionClient.Models;

namespace InspectionClient.Interfaces;

/// <summary>
/// WaferInfo 테이블 CRUD 계약.
/// </summary>
public interface IWaferInfoRepository : INamedRepository<WaferInfoRow>
{
}
