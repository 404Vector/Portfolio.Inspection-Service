using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.Models;

namespace InspectionClient.Interfaces;

/// <summary>DB에서 조회된 UserAnnotation과 그 식별자를 묶는 항목.</summary>
public sealed record UserAnnotationEntry(long Id, UserAnnotation Annotation);

/// <summary>
/// UserAnnotation 영속성 계약.
/// </summary>
public interface IUserAnnotationRepository
{
  Task SaveAsync(UserAnnotation annotation, CancellationToken ct = default);
  Task<IReadOnlyList<UserAnnotationEntry>> ListAsync(string entityId, EntityKind kind, CancellationToken ct = default);
  Task DeleteAsync(long id, CancellationToken ct = default);
}
