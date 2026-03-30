namespace Core.Models;

/// <summary>
/// 웨이퍼 Die 격자상의 정수 인덱스 식별자입니다.
/// SEMI E142 관례에 따라 (Col, Row) 쌍으로 Die를 고유하게 식별합니다.
/// Col은 +X 방향, Row는 +Y 방향(위쪽)으로 증가합니다.
/// </summary>
public readonly struct DieIndex : IEquatable<DieIndex>, IComparable<DieIndex> {
  /// <summary>열 인덱스. +X 방향으로 증가.</summary>
  public int Col { get; }

  /// <summary>행 인덱스. +Y 방향(위쪽)으로 증가.</summary>
  public int Row { get; }

  public DieIndex(int col, int row) {
    Col = col;
    Row = row;
  }

  /// <summary>
  /// 문자열 표현. 예: "C+3R-2", "C+0R+0"
  /// </summary>
  public override string ToString() => $"C{Col:+#;-#;+0}R{Row:+#;-#;+0}";

  /// <summary>Row 우선, Col 보조로 정렬합니다 (위에서 아래, 왼쪽에서 오른쪽).</summary>
  public int CompareTo(DieIndex other) {
    int rowCmp = Row.CompareTo(other.Row);
    return rowCmp != 0 ? rowCmp : Col.CompareTo(other.Col);
  }

  public bool Equals(DieIndex other) => Col == other.Col && Row == other.Row;

  public override bool Equals(object? obj) => obj is DieIndex other && Equals(other);

  public override int GetHashCode() => HashCode.Combine(Col, Row);

  public static bool operator ==(DieIndex left, DieIndex right) => left.Equals(right);
  public static bool operator !=(DieIndex left, DieIndex right) => !left.Equals(right);
}
