using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.Models;

namespace InspectionClient.Interfaces;

/// <summary>
/// WaferInfo 영속성 계약.
/// </summary>
public interface IWaferInfoRepository
{
  Task SaveAsync(WaferInfo waferInfo, CancellationToken ct = default);
  Task<WaferInfo?> FindAsync(string waferId, CancellationToken ct = default);
  Task<IReadOnlyList<WaferInfo>> ListAsync(CancellationToken ct = default);
  Task DeleteAsync(string waferId, CancellationToken ct = default);
}
