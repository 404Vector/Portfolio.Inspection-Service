namespace Core.Models;

/// <summary>
/// <see cref="WaferInfo"/>로부터 계산된 웨이퍼의 유효 Die 레이아웃 맵입니다.
/// Die 유효성 판정은 Die 중심이 웨이퍼 원 안에 있는지를 기준으로 합니다.
/// </summary>
public record DieMap {
  /// <summary>이 DieMap이 파생된 웨이퍼 정보</summary>
  public WaferInfo Wafer { get; }

  /// <summary>
  /// 유효 Die 목록. DieIndex(Col, Row)로 조회 가능합니다.
  /// Row 내림차순(위→아래), Col 오름차순(왼→오) 순서로 정렬됩니다.
  /// </summary>
  public IReadOnlyList<DieRegion> Dies { get; }

  /// <summary>유효 Die 총 개수</summary>
  public int DieCount => Dies.Count;

  private DieMap(WaferInfo wafer, IReadOnlyList<DieRegion> dies) {
    Wafer = wafer;
    Dies  = dies;
  }

  /// <summary>
  /// WaferInfo로부터 DieMap을 계산하여 생성합니다.
  /// </summary>
  /// <param name="wafer">레이아웃을 계산할 웨이퍼 정보</param>
  /// <returns>유효 Die 목록이 채워진 DieMap</returns>
  public static DieMap From(WaferInfo wafer) {
    double radiusUm  = wafer.RadiusUm;
    double widthUm   = wafer.DieSize.WidthUm;
    double heightUm  = wafer.DieSize.HeightUm;
    double offsetXum = wafer.DieOffset.Xum;
    double offsetYum = wafer.DieOffset.Yum;

    // Die 격자 탐색 범위: 웨이퍼 반지름을 Die 크기로 나눠 최대 인덱스를 결정합니다.
    int colMin = (int)Math.Floor((-radiusUm - offsetXum) / widthUm);
    int colMax = (int)Math.Ceiling((radiusUm - offsetXum) / widthUm);
    int rowMin = (int)Math.Floor((-radiusUm - offsetYum) / heightUm);
    int rowMax = (int)Math.Ceiling((radiusUm - offsetYum) / heightUm);

    double radiusSq = radiusUm * radiusUm;

    var dies = new List<DieRegion>();

    // Row 내림차순(위→아래)으로 순회하여 시각적으로 자연스러운 순서를 보장합니다.
    for (int row = rowMax; row >= rowMin; row--) {
      for (int col = colMin; col <= colMax; col++) {
        double bottomLeftX = offsetXum + col * widthUm;
        double bottomLeftY = offsetYum + row * heightUm;

        double centerX = bottomLeftX + widthUm / 2.0;
        double centerY = bottomLeftY + heightUm / 2.0;

        // 유효성 판정: Die 중심이 웨이퍼 원 안에 있어야 합니다.
        if (centerX * centerX + centerY * centerY > radiusSq) {
          continue;
        }

        var index  = new DieIndex(col, row);
        var origin = new WaferCoordinate(bottomLeftX, bottomLeftY);
        dies.Add(new DieRegion(index, origin, widthUm, heightUm));
      }
    }

    return new DieMap(wafer, dies.AsReadOnly());
  }

  /// <summary>
  /// 주어진 DieIndex에 해당하는 Die를 반환합니다.
  /// 존재하지 않으면 null을 반환합니다.
  /// </summary>
  public DieRegion? FindByIndex(DieIndex index) {
    foreach (var die in Dies) {
      if (die.Index == index) return die;
    }
    return null;
  }

  /// <summary>
  /// 웨이퍼 좌표(µm)를 포함하는 Die를 반환합니다.
  /// 해당 좌표가 어느 Die에도 속하지 않으면 null을 반환합니다.
  /// </summary>
  public DieRegion? FindByCoordinate(WaferCoordinate point) {
    foreach (var die in Dies) {
      if (die.Contains(point)) return die;
    }
    return null;
  }
}
