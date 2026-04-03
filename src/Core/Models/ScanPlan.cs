using Core.Enums;

namespace Core.Models;

/// <summary>
/// 웨이퍼 전체에 대한 Boustrophedon 스캔 계획입니다.
/// Die 격자 경계에 Shot 중심을 정렬하여 die-to-die 알고리즘에 적합한 반복 패턴을 보장합니다.
/// FOV가 Die보다 크거나 작은 경우를 모두 지원합니다.
/// </summary>
public record ScanPlan {
  /// <summary>유효 Die 레이아웃</summary>
  public DieMap DieMap { get; }

  /// <summary>카메라 FOV 크기 (µm)</summary>
  public FovSize Fov { get; }

  /// <summary>Shot 간 X축 오버랩 (µm). 0이면 오버랩 없음.</summary>
  public double OverlapXum { get; }

  /// <summary>Sector 간 Y축 오버랩 (µm). 0이면 오버랩 없음.</summary>
  public double OverlapYum { get; }

  /// <summary>
  /// 웨이퍼 면에서의 픽셀 크기 (µm/pixel).
  /// CameraImageSensorPixelSize / LensMagnification으로 결정됩니다.
  /// </summary>
  public double PixelSizeUm { get; }

  /// <summary>
  /// Sector 목록. 웨이퍼 하단(−Y)부터 상단(+Y) 순서로 정렬됩니다.
  /// 짝수 인덱스 Sector는 LeftToRight, 홀수는 RightToLeft (Boustrophedon).
  /// </summary>
  public IReadOnlyList<ScanSector> Sectors { get; }

  /// <summary>전체 Shot 총 개수</summary>
  public int TotalShotCount => Sectors.Sum(s => s.ShotCount);

  [System.Text.Json.Serialization.JsonConstructor]
  public ScanPlan(
    DieMap                    dieMap,
    FovSize                   fov,
    double                    overlapXum,
    double                    overlapYum,
    double                    pixelSizeUm,
    IReadOnlyList<ScanSector> sectors
  ) {
    if (pixelSizeUm <= 0) throw new ArgumentOutOfRangeException(nameof(pixelSizeUm), "픽셀 크기는 0보다 커야 합니다.");
    DieMap      = dieMap;
    Fov         = fov;
    OverlapXum  = overlapXum;
    OverlapYum  = overlapYum;
    PixelSizeUm = pixelSizeUm;
    Sectors     = sectors;
  }

  /// <summary>
  /// WaferInfo와 FOV 크기, 오버랩 설정으로부터 ScanPlan을 계산하여 생성합니다.
  /// </summary>
  /// <param name="wafer">스캔 대상 웨이퍼</param>
  /// <param name="fov">카메라 FOV 크기</param>
  /// <param name="overlapXum">Shot 간 X축 오버랩 (µm, 기본값 0)</param>
  /// <param name="overlapYum">Sector 간 Y축 오버랩 (µm, 기본값 0)</param>
  public static ScanPlan From(
    WaferInfo wafer,
    FovSize   fov,
    double    pixelSizeUm,
    double    overlapXum = 0.0,
    double    overlapYum = 0.0
  ) {
    if (overlapXum < 0) throw new ArgumentOutOfRangeException(nameof(overlapXum), "오버랩은 0 이상이어야 합니다.");
    if (overlapYum < 0) throw new ArgumentOutOfRangeException(nameof(overlapYum), "오버랩은 0 이상이어야 합니다.");
    if (overlapXum >= fov.WidthUm)  throw new ArgumentOutOfRangeException(nameof(overlapXum), "X 오버랩이 FOV 너비 이상입니다.");
    if (overlapYum >= fov.HeightUm) throw new ArgumentOutOfRangeException(nameof(overlapYum), "Y 오버랩이 FOV 높이 이상입니다.");

    var dieMap   = DieMap.From(wafer);
    var sectors  = BuildSectors(wafer, dieMap, fov, overlapXum, overlapYum);

    return new ScanPlan(dieMap, fov, overlapXum, overlapYum, pixelSizeUm, sectors);
  }

  // ── 내부 계산 ──────────────────────────────────────────────────────────

  private static IReadOnlyList<ScanSector> BuildSectors(
    WaferInfo wafer,
    DieMap    dieMap,
    FovSize   fov,
    double    overlapXum,
    double    overlapYum
  ) {
    double dieW    = wafer.DieSize.WidthUm;
    double dieH    = wafer.DieSize.HeightUm;
    double offsetX = wafer.DieOffset.Xum;
    double offsetY = wafer.DieOffset.Yum;
    double radiusUm = wafer.RadiusUm;

    // Shot 이동 피치: FOV 크기에서 오버랩을 뺀 거리
    double pitchX = fov.WidthUm  - overlapXum;
    double pitchY = fov.HeightUm - overlapYum;

    // ── Y축 Shot 중심 목록 계산 ─────────────────────────────────────────
    // Shot 중심은 Die 경계에 정렬됩니다.
    // Die 경계 Y좌표: offsetY + n * dieH  (n은 정수)
    // FOV pitchY 단위로 Die 경계를 샘플링합니다.
    var sectorCentersY = ComputeGridAlignedCenters(
      offsetY, dieH, pitchY, fov.HeightUm, radiusUm
    );

    var sectors = new List<ScanSector>();

    for (int si = 0; si < sectorCentersY.Count; si++) {
      double centerY  = sectorCentersY[si];
      var direction   = si % 2 == 0 ? ScanDirection.LeftToRight : ScanDirection.RightToLeft;

      // ── X축 Shot 중심 목록 계산 ────────────────────────────────────────
      var shotCentersX = ComputeGridAlignedCenters(
        offsetX, dieW, pitchX, fov.WidthUm, radiusUm
      );

      // Boustrophedon: RightToLeft Sector는 X 역순
      if (direction == ScanDirection.RightToLeft) {
        shotCentersX = Enumerable.Reverse(shotCentersX).ToList();
      }

      var shots = new List<ScanShot>();

      for (int xi = 0; xi < shotCentersX.Count; xi++) {
        double centerX = shotCentersX[xi];
        var center     = new WaferCoordinate(centerX, centerY);
        var shot       = new ScanShot(xi, center, fov, FindCoveredDies(center, fov, dieMap));

        // Shot FOV가 웨이퍼 원과 겹치는 경우만 포함 (전체가 원 밖인 Shot 제외)
        if (shot.CoveredDies.Count > 0 || FovOverlapsWafer(center, fov, radiusUm)) {
          shots.Add(shot);
        }
      }

      if (shots.Count > 0) {
        sectors.Add(new ScanSector(si, direction, centerY, shots.AsReadOnly()));
      }
    }

    return sectors.AsReadOnly();
  }

  /// <summary>
  /// Die 격자 경계에 정렬된 Shot 중심 좌표 목록을 계산합니다.
  /// </summary>
  /// <param name="dieGridOffset">Die 격자 원점 오프셋 (µm)</param>
  /// <param name="dieStep">Die 크기 (µm) — 격자 경계 간격</param>
  /// <param name="shotPitch">Shot 이동 피치 (µm)</param>
  /// <param name="fovSpan">FOV 크기 (µm) — 웨이퍼 범위 계산용</param>
  /// <param name="radiusUm">웨이퍼 반지름 (µm)</param>
  private static List<double> ComputeGridAlignedCenters(
    double dieGridOffset,
    double dieStep,
    double shotPitch,
    double fovSpan,
    double radiusUm
  ) {
    // Die 격자 경계 중 웨이퍼 범위 안에 있는 것을 열거합니다.
    // 경계 인덱스 n에 대해 경계 좌표 = dieGridOffset + n * dieStep
    int nMin = (int)Math.Floor((-radiusUm - dieGridOffset) / dieStep);
    int nMax = (int)Math.Ceiling((radiusUm - dieGridOffset) / dieStep);

    // shotPitch 간격으로 Die 경계를 샘플링하여 Shot 중심을 결정합니다.
    // Shot 중심 = Die 경계 + FOV/2 (경계가 Shot 좌측/하단 기준)
    var centers = new SortedSet<double>();

    for (int n = nMin; n <= nMax; n++) {
      double boundary = dieGridOffset + n * dieStep;

      // 이 경계에서 shotPitch 배수만큼 떨어진 경계들도 Shot 중심 후보
      // 가장 가까운 pitchX 배수 경계로 스냅
      double snapped = Math.Round((boundary - dieGridOffset) / shotPitch) * shotPitch + dieGridOffset;
      double center  = snapped + fovSpan / 2.0;
      centers.Add(center);
    }

    return centers
      .Where(c => Math.Abs(c) - fovSpan / 2.0 < radiusUm)
      .ToList();
  }

  /// <summary>
  /// Shot FOV와 겹치는 Die 인덱스 목록을 DieMap에서 조회합니다.
  /// </summary>
  private static IReadOnlyList<DieIndex> FindCoveredDies(
    WaferCoordinate center,
    FovSize         fov,
    DieMap          dieMap
  ) {
    var shot    = new ScanShot(0, center, fov, Array.Empty<DieIndex>());
    var covered = new List<DieIndex>();

    foreach (var die in dieMap.Dies) {
      if (shot.Overlaps(die)) {
        covered.Add(die.Index);
      }
    }

    return covered.AsReadOnly();
  }

  /// <summary>
  /// Shot FOV가 웨이퍼 원과 조금이라도 겹치는지 확인합니다.
  /// FOV AABB의 웨이퍼 중심과의 최소 거리를 계산하여 판정합니다.
  /// </summary>
  private static bool FovOverlapsWafer(WaferCoordinate center, FovSize fov, double radiusUm) {
    double halfW = fov.WidthUm  / 2.0;
    double halfH = fov.HeightUm / 2.0;

    // AABB와 원의 교차 판정: 원 중심(0,0)에서 AABB 표면까지 최소 거리
    double dx = Math.Max(0, Math.Abs(center.Xum) - halfW);
    double dy = Math.Max(0, Math.Abs(center.Yum) - halfH);
    return dx * dx + dy * dy < radiusUm * radiusUm;
  }
}
