using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.Models;
using InspectionClient.Interfaces;
using InspectionClient.Services;

namespace InspectionClient.Repositories;

public sealed class SqliteUserAnnotationRepository : IUserAnnotationRepository
{
  private readonly InspectionDatabase _db;

  public SqliteUserAnnotationRepository(InspectionDatabase db)
  {
    _db = db;
  }

  public async Task SaveAsync(UserAnnotation annotation, CancellationToken ct = default)
  {
    ArgumentNullException.ThrowIfNull(annotation);

    await using var cmd = _db.Connection.CreateCommand();
    cmd.CommandText = """
      INSERT INTO UserAnnotation (EntityId, EntityKind, Operator, Comment, Tags, CreatedAt)
      VALUES ($entityId, $entityKind, $operator, $comment, $tags, $createdAt)
      """;
    cmd.Parameters.AddWithValue("$entityId",   annotation.EntityId);
    cmd.Parameters.AddWithValue("$entityKind", annotation.EntityKind.ToString());
    cmd.Parameters.AddWithValue("$operator",   annotation.Operator);
    cmd.Parameters.AddWithValue("$comment",    annotation.Comment);
    cmd.Parameters.AddWithValue("$tags",       string.Join(",", annotation.Tags));
    cmd.Parameters.AddWithValue("$createdAt",  annotation.CreatedAt.ToString("O"));
    await cmd.ExecuteNonQueryAsync(ct);
  }

  public async Task<IReadOnlyList<UserAnnotationEntry>> ListAsync(
      string entityId,
      EntityKind kind,
      CancellationToken ct = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(entityId);

    await using var cmd = _db.Connection.CreateCommand();
    cmd.CommandText = """
      SELECT Id, EntityId, EntityKind, Operator, Comment, Tags, CreatedAt
      FROM UserAnnotation
      WHERE EntityId = $entityId AND EntityKind = $entityKind
      ORDER BY CreatedAt DESC
      """;
    cmd.Parameters.AddWithValue("$entityId",   entityId);
    cmd.Parameters.AddWithValue("$entityKind", kind.ToString());

    var list = new List<UserAnnotationEntry>();
    await using var reader = await cmd.ExecuteReaderAsync(ct);
    while (await reader.ReadAsync(ct))
    {
      var id   = reader.GetInt64(0);
      var tags = reader.GetString(5)
          .Split(',', StringSplitOptions.RemoveEmptyEntries);

      var annotation = new UserAnnotation(
        EntityId:   reader.GetString(1),
        EntityKind: Enum.Parse<EntityKind>(reader.GetString(2)),
        Operator:   reader.GetString(3),
        Comment:    reader.GetString(4),
        Tags:       tags,
        CreatedAt:  DateTimeOffset.Parse(reader.GetString(6))
      );
      list.Add(new UserAnnotationEntry(id, annotation));
    }

    return list;
  }

  public async Task DeleteAsync(long id, CancellationToken ct = default)
  {
    await using var cmd = _db.Connection.CreateCommand();
    cmd.CommandText = "DELETE FROM UserAnnotation WHERE Id = $id";
    cmd.Parameters.AddWithValue("$id", id);
    await cmd.ExecuteNonQueryAsync(ct);
  }
}
