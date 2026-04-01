using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace InspectionClient.Interfaces;

/// <summary>
/// Id(AUTOINCREMENT) + Name(UNIQUE) 기반 테이블의 공통 CRUD 계약.
/// </summary>
public interface INamedRepository<T>
{
  /// <summary>지정한 이름으로 기본값 row를 생성하고 반환한다. Id는 DB가 자동 할당한다.</summary>
  Task<T> CreateAsync(string name, CancellationToken ct = default);

  /// <summary>Id로 row를 조회한다.</summary>
  Task<T?> FindByIdAsync(long id, CancellationToken ct = default);

  /// <summary>전체 목록을 반환한다.</summary>
  Task<IReadOnlyList<T>> ListAsync(CancellationToken ct = default);

  /// <summary>기존 row를 갱신한다. Id를 기준으로 UPDATE한다.</summary>
  Task UpdateAsync(T item, CancellationToken ct = default);

  /// <summary>Id로 row를 삭제한다.</summary>
  Task DeleteAsync(long id, CancellationToken ct = default);
}
