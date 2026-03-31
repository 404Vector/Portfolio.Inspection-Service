using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace InspectionClient.Services;

/// <summary>
/// 검증용 SQLite DB 연결 및 스키마 관리.
///
/// NOTE: 검증(Validation) 전용입니다. 실행파일과 같은 위치에 inspection.db를 생성합니다.
/// 프로덕션 환경에서는 적절한 위치의 DB 또는 별도 서버 DB로 교체해야 합니다.
///
/// 스키마 버전 불일치 시 DB 파일을 삭제하고 재생성합니다. 기존 데이터는 소실됩니다.
/// </summary>
public sealed class InspectionDatabase : IDisposable
{
  // 스키마 변경 시 이 값을 증가시키면 다음 실행 시 DB가 자동으로 재생성됩니다.
  private const int SchemaVersion = 2;

  private SqliteConnection _connection;

  public SqliteConnection Connection => _connection;

  public InspectionDatabase()
  {
    var dbPath = Path.Combine(AppContext.BaseDirectory, "inspection.db");
    _connection = OpenConnection(dbPath);

    if (!IsSchemaVersionMatch())
    {
      _connection.Dispose();
      SqliteConnection.ClearAllPools();
      File.Delete(dbPath);
      _connection = OpenConnection(dbPath);
    }

    CreateTables();
    SetSchemaVersion();
    SeedIfEmpty();
  }

  private static SqliteConnection OpenConnection(string dbPath)
  {
    var conn = new SqliteConnection($"Data Source={dbPath}");
    conn.Open();
    // 저널 모드를 DELETE로 명시하여 WAL 관련 I/O 이슈를 방지합니다.
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "PRAGMA journal_mode=DELETE";
    cmd.ExecuteScalar();
    return conn;
  }

  // ── 스키마 버전 ────────────────────────────────────────────────────────

  private bool IsSchemaVersionMatch()
  {
    using var cmd = _connection.CreateCommand();
    cmd.CommandText = "PRAGMA user_version";
    var version = Convert.ToInt32(cmd.ExecuteScalar());
    return version == SchemaVersion;
  }

  private void SetSchemaVersion()
  {
    using var cmd = _connection.CreateCommand();
    // PRAGMA user_version은 파라미터 바인딩을 지원하지 않으므로 직접 삽입합니다.
    // SchemaVersion은 상수이므로 SQL 인젝션 위험이 없습니다.
    cmd.CommandText = $"PRAGMA user_version = {SchemaVersion}";
    cmd.ExecuteNonQuery();
  }

  // ── 테이블 생성 ───────────────────────────────────────────────────────

  private void CreateTables()
  {
    // Microsoft.Data.Sqlite는 단일 명령어만 지원하므로 각 DDL을 개별 실행합니다.
    ExecuteNonQuery("""
      CREATE TABLE IF NOT EXISTS WaferInfo (
        WaferId     TEXT    NOT NULL PRIMARY KEY,
        LotId       TEXT    NOT NULL,
        WaferType   TEXT    NOT NULL,
        CreatedAt   TEXT    NOT NULL,
        Json        TEXT    NOT NULL
      )
      """);

    ExecuteNonQuery("""
      CREATE TABLE IF NOT EXISTS Recipe (
        RecipeName  TEXT    NOT NULL PRIMARY KEY,
        WaferId     TEXT    NOT NULL,
        CreatedAt   TEXT    NOT NULL,
        Json        TEXT    NOT NULL
      )
      """);

    ExecuteNonQuery("""
      CREATE TABLE IF NOT EXISTS InspectionResult (
        ResultId    TEXT    NOT NULL PRIMARY KEY,
        RecipeName  TEXT    NOT NULL,
        WaferId     TEXT    NOT NULL,
        Status      TEXT    NOT NULL,
        StartedAt   TEXT    NOT NULL,
        CompletedAt TEXT    NOT NULL,
        Json        TEXT    NOT NULL
      )
      """);

    ExecuteNonQuery("""
      CREATE TABLE IF NOT EXISTS UserAnnotation (
        Id          INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
        EntityId    TEXT    NOT NULL,
        EntityKind  TEXT    NOT NULL,
        Operator    TEXT    NOT NULL,
        Comment     TEXT    NOT NULL,
        Tags        TEXT    NOT NULL,
        CreatedAt   TEXT    NOT NULL
      )
      """);

    ExecuteNonQuery("""
      CREATE INDEX IF NOT EXISTS idx_annotation_entity
        ON UserAnnotation (EntityId, EntityKind)
      """);

    ExecuteNonQuery("""
      CREATE TABLE IF NOT EXISTS DieRenderingParameters (
        Name  TEXT NOT NULL PRIMARY KEY,
        Json  TEXT NOT NULL
      )
      """);
  }

  private void ExecuteNonQuery(string sql)
  {
    using var cmd = _connection.CreateCommand();
    cmd.CommandText = sql;
    cmd.ExecuteNonQuery();
  }

  // ── 더미 데이터 삽입 ──────────────────────────────────────────────────

  private void SeedIfEmpty()
  {
    if (!IsTableEmpty("WaferInfo"))
      return;

    using var tx = _connection.BeginTransaction();

    SeedDieRenderingParameters(tx);
    SeedWaferInfo(tx);
    SeedRecipe(tx);
    SeedInspectionResult(tx);
    SeedUserAnnotations(tx);

    tx.Commit();
  }

  private bool IsTableEmpty(string tableName)
  {
    using var cmd = _connection.CreateCommand();
    // tableName은 내부 상수 문자열만 사용하므로 SQL 인젝션 위험이 없습니다.
    cmd.CommandText = $"SELECT COUNT(*) FROM {tableName}";
    return Convert.ToInt64(cmd.ExecuteScalar()) == 0;
  }

  private void SeedDieRenderingParameters(SqliteTransaction tx)
  {
    var json = Serialize(new
    {
      canvasWidth         = 10_000,
      canvasHeight        = 10_000,
      backgroundGray      = 70,
      showAlignmentMarks  = true,
      showRuler           = true,
      showTextureBands    = true,
      showCalibrationMark = true,
      padRowCount         = 1,
      padColumnCount      = 6,
    });

    using var cmd = _connection.CreateCommand();
    cmd.Transaction = tx;
    cmd.CommandText = """
      INSERT INTO DieRenderingParameters (Name, Json)
      VALUES ($name, $json)
      """;
    cmd.Parameters.AddWithValue("$name", "SEED-DEFAULT");
    cmd.Parameters.AddWithValue("$json", json);
    cmd.ExecuteNonQuery();
  }

  private void SeedWaferInfo(SqliteTransaction tx)
  {
    var dummy = Core.Models.WaferInfo.CreateDummy();
    var json  = Serialize(dummy);

    using var cmd = _connection.CreateCommand();
    cmd.Transaction = tx;
    cmd.CommandText = """
      INSERT INTO WaferInfo (WaferId, LotId, WaferType, CreatedAt, Json)
      VALUES ($waferId, $lotId, $waferType, $createdAt, $json)
      """;
    cmd.Parameters.AddWithValue("$waferId",   dummy.WaferId);
    cmd.Parameters.AddWithValue("$lotId",     dummy.LotId);
    cmd.Parameters.AddWithValue("$waferType", dummy.WaferType.ToString());
    cmd.Parameters.AddWithValue("$createdAt", dummy.CreatedAt.ToString("O"));
    cmd.Parameters.AddWithValue("$json",      json);
    cmd.ExecuteNonQuery();
  }

  private void SeedRecipe(SqliteTransaction tx)
  {
    var wafer  = Core.Models.WaferInfo.CreateDummy();
    var recipe = new InspectionRecipe.Models.WaferSurfaceInspectionRecipe(
      RecipeName:      "SEED-RECIPE",
      Description:     "Seed dummy recipe",
      Wafer:           wafer,
      Fov:             new Core.Models.FovSize(1413.0, 1035.0)
    );
    var json      = Serialize(recipe);
    var createdAt = DateTimeOffset.UtcNow.ToString("O");

    using var cmd = _connection.CreateCommand();
    cmd.Transaction = tx;
    cmd.CommandText = """
      INSERT INTO Recipe (RecipeName, WaferId, CreatedAt, Json)
      VALUES ($recipeName, $waferId, $createdAt, $json)
      """;
    cmd.Parameters.AddWithValue("$recipeName", recipe.RecipeName);
    cmd.Parameters.AddWithValue("$waferId",    recipe.Wafer.WaferId);
    cmd.Parameters.AddWithValue("$createdAt",  createdAt);
    cmd.Parameters.AddWithValue("$json",       json);
    cmd.ExecuteNonQuery();
  }

  private void SeedInspectionResult(SqliteTransaction tx)
  {
    var resultId  = Guid.NewGuid().ToString();
    var startedAt = DateTimeOffset.UtcNow;
    var result    = new Core.Models.WaferSurfaceInspectionResult(
      RecipeName:   "SEED-RECIPE",
      Wafer:        Core.Models.WaferInfo.CreateDummy(),
      Status:       Core.Enums.InspectionStatus.Pass,
      StartedAt:    startedAt,
      CompletedAt:  startedAt.AddSeconds(1),
      FrameResults: Array.Empty<Core.Models.FrameInspectionResult>()
    );
    var json = Serialize(result);

    using var cmd = _connection.CreateCommand();
    cmd.Transaction = tx;
    cmd.CommandText = """
      INSERT INTO InspectionResult
        (ResultId, RecipeName, WaferId, Status, StartedAt, CompletedAt, Json)
      VALUES
        ($resultId, $recipeName, $waferId, $status, $startedAt, $completedAt, $json)
      """;
    cmd.Parameters.AddWithValue("$resultId",   resultId);
    cmd.Parameters.AddWithValue("$recipeName", result.RecipeName);
    cmd.Parameters.AddWithValue("$waferId",    result.Wafer.WaferId);
    cmd.Parameters.AddWithValue("$status",     result.Status.ToString());
    cmd.Parameters.AddWithValue("$startedAt",  result.StartedAt.ToString("O"));
    cmd.Parameters.AddWithValue("$completedAt",result.CompletedAt.ToString("O"));
    cmd.Parameters.AddWithValue("$json",       json);
    cmd.ExecuteNonQuery();

    // InspectionResult의 ResultId를 UserAnnotation에서 참조하기 위해 반환
    SeedAnnotation(tx, resultId, Core.Models.EntityKind.InspectionResult);
  }

  private void SeedUserAnnotations(SqliteTransaction tx)
  {
    SeedAnnotation(tx, "WAFER-DUMMY",  Core.Models.EntityKind.WaferInfo);
    SeedAnnotation(tx, "SEED-RECIPE",  Core.Models.EntityKind.Recipe);
    // InspectionResult 주석은 SeedInspectionResult에서 resultId와 함께 삽입
  }

  private void SeedAnnotation(SqliteTransaction tx, string entityId, Core.Models.EntityKind kind)
  {
    using var cmd = _connection.CreateCommand();
    cmd.Transaction = tx;
    cmd.CommandText = """
      INSERT INTO UserAnnotation (EntityId, EntityKind, Operator, Comment, Tags, CreatedAt)
      VALUES ($entityId, $entityKind, $operator, $comment, $tags, $createdAt)
      """;
    cmd.Parameters.AddWithValue("$entityId",   entityId);
    cmd.Parameters.AddWithValue("$entityKind", kind.ToString());
    cmd.Parameters.AddWithValue("$operator",   "seed");
    cmd.Parameters.AddWithValue("$comment",    "Seed dummy annotation");
    cmd.Parameters.AddWithValue("$tags",       "seed,dummy");
    cmd.Parameters.AddWithValue("$createdAt",  DateTimeOffset.UtcNow.ToString("O"));
    cmd.ExecuteNonQuery();
  }

  // ── JSON 직렬화 헬퍼 ──────────────────────────────────────────────────

  private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
  {
    WriteIndented = false,
    Converters    = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
  };

  private static string Serialize<T>(T value) =>
      System.Text.Json.JsonSerializer.Serialize(value, JsonOptions);

  // ── IDisposable ───────────────────────────────────────────────────────

  public void Dispose() => _connection.Dispose();
}
