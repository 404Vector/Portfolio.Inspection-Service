using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.Models;

namespace InspectionClient.Interfaces;

/// <summary>
/// ResultId와 결과 데이터를 함께 제공하는 목록 항목.
/// </summary>
public sealed record InspectionResultEntry(string ResultId, WaferSurfaceInspectionResult Result);

/// <summary>
/// WaferSurfaceInspectionResult 영속성 계약.
/// </summary>
public interface IInspectionResultRepository
{
  /// <summary>검사 결과를 저장하고 생성된 ResultId를 반환한다.</summary>
  Task<string> SaveAsync(WaferSurfaceInspectionResult result, CancellationToken ct = default);
  Task<WaferSurfaceInspectionResult?> FindAsync(string resultId, CancellationToken ct = default);
  Task<IReadOnlyList<WaferSurfaceInspectionResult>> ListAsync(CancellationToken ct = default);
  Task<IReadOnlyList<InspectionResultEntry>> ListEntriesAsync(CancellationToken ct = default);
  Task DeleteAsync(string resultId, CancellationToken ct = default);
}
