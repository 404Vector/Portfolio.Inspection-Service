using System.Threading;
using System.Threading.Tasks;
using InspectionClient.Models;

namespace InspectionClient.Interfaces;

/// <summary>
/// WaferInfo 테이블 CRUD 계약.
/// </summary>
public interface IWaferInfoRepository : INamedRepository<WaferInfoRow>
{
  /// <summary>WaferId(도메인 식별자)로 row를 조회한다. 없으면 null을 반환한다.</summary>
  Task<WaferInfoRow?> FindByWaferIdAsync(string waferId, CancellationToken ct = default);
}
