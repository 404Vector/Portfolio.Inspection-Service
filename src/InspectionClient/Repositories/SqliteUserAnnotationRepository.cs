using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.Models;
using InspectionClient.Interfaces;
using InspectionClient.Services;
using Microsoft.Data.Sqlite;

namespace InspectionClient.Repositories;

public sealed class SqliteUserAnnotationRepository : IUserAnnotationRepository
{
  private readonly InspectionDatabase _db;

  public SqliteUserAnnotationRepository(InspectionDatabase db)
  {
    _db = db;
  }

  public Task SaveAsync(UserAnnotation annotation, CancellationToken ct = default)
  {
    ArgumentNullException.ThrowIfNull(annotation);

    using var cmd = _db.Connection.CreateCommand();
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
    cmd.ExecuteNonQuery();

    return Task.CompletedTask;
  }

  public Task<IReadOnlyList<UserAnnotation>> ListAsync(
      string entityId,
      EntityKind kind,
      CancellationToken ct = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(entityId);

    using var cmd = _db.Connection.CreateCommand();
    cmd.CommandText = """
      SELECT EntityId, EntityKind, Operator, Comment, Tags, CreatedAt
      FROM UserAnnotation
      WHERE EntityId = $entityId AND EntityKind = $entityKind
      ORDER BY CreatedAt DESC
      """;
    cmd.Parameters.AddWithValue("$entityId",   entityId);
    cmd.Parameters.AddWithValue("$entityKind", kind.ToString());

    var list = new List<UserAnnotation>();
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
      var tags = reader.GetString(4)
          .Split(',', StringSplitOptions.RemoveEmptyEntries);

      list.Add(new UserAnnotation(
        EntityId:   reader.GetString(0),
        EntityKind: Enum.Parse<EntityKind>(reader.GetString(1)),
        Operator:   reader.GetString(2),
        Comment:    reader.GetString(3),
        Tags:       tags,
        CreatedAt:  DateTimeOffset.Parse(reader.GetString(5))
      ));
    }

    return Task.FromResult<IReadOnlyList<UserAnnotation>>(list);
  }

  public Task DeleteAsync(long id, CancellationToken ct = default)
  {
    using var cmd = _db.Connection.CreateCommand();
    cmd.CommandText = "DELETE FROM UserAnnotation WHERE Id = $id";
    cmd.Parameters.AddWithValue("$id", id);
    cmd.ExecuteNonQuery();

    return Task.CompletedTask;
  }
}
