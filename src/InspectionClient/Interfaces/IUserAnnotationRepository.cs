using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.Models;

namespace InspectionClient.Interfaces;

/// <summary>
/// UserAnnotation 영속성 계약.
/// </summary>
public interface IUserAnnotationRepository
{
  Task SaveAsync(UserAnnotation annotation, CancellationToken ct = default);
  Task<IReadOnlyList<UserAnnotation>> ListAsync(string entityId, EntityKind kind, CancellationToken ct = default);
  Task DeleteAsync(long id, CancellationToken ct = default);
}
