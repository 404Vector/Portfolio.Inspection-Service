using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InspectionClient.Models;

namespace InspectionClient.Interfaces;

/// <summary>이름과 파라미터를 함께 제공하는 목록 항목.</summary>
public sealed record DieParametersEntry(string Name, DieRenderingParameters Parameters);

/// <summary>
/// DieRenderingParameters 영속성 계약.
/// </summary>
public interface IDieRenderingParametersRepository
{
  Task SaveAsync(string name, DieRenderingParameters parameters, CancellationToken ct = default);
  Task<DieRenderingParameters?> FindAsync(string name, CancellationToken ct = default);
  Task<IReadOnlyList<DieParametersEntry>> ListAsync(CancellationToken ct = default);
  Task DeleteAsync(string name, CancellationToken ct = default);
}
