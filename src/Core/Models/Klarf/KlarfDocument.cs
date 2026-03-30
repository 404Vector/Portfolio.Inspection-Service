namespace Core.Models.Klarf;

/// <summary>
/// KLARF 1.8 파일 전체를 나타내는 최상위 집계 루트입니다.
/// 각 필드는 KLARF 섹션과 1:1로 대응하며, 직렬화 계층(KlarfWriter)이
/// 이 모델을 소비하여 KLARF 텍스트를 생성합니다.
/// </summary>
/// <param name="FileVersion">KLARF 파일 포맷 버전. 통상 "1 8". KLARF FileVersion.</param>
/// <param name="FileTimestamp">파일 생성 시각. KLARF FileTimestamp.</param>
/// <param name="Station">검사 장비 식별 정보. KLARF InspectionStationID.</param>
/// <param name="Setup">검사 설정 및 레시피 메타. KLARF SetupID / InspectionTest.</param>
/// <param name="Wafer">검사 대상 웨이퍼 정보. KLARF LotID, WaferID, SampleSize 등.</param>
/// <param name="Defects">전체 결함 목록. KLARF DefectList. 모든 프레임의 결함을 순서대로 담습니다.</param>
/// <param name="TotalDefectCount">요약 결함 수. KLARF Summary 섹션의 NumberOfDefects.</param>
public record KlarfDocument(
  string                           FileVersion,
  DateTimeOffset                   FileTimestamp,
  KlarfInspectionStation           Station,
  KlarfInspectionSetup             Setup,
  WaferInfo                        Wafer,
  IReadOnlyList<DefectInfo>        Defects,
  int                              TotalDefectCount
) {
  /// <summary>KLARF 1.8 고정 포맷 버전 문자열</summary>
  public static string CurrentVersion => "1 8";

  /// <summary>
  /// WaferSurfaceInspectionResult로부터 KlarfDocument를 생성합니다.
  /// </summary>
  /// <param name="result">검사 잡 결과</param>
  /// <param name="station">검사 장비 정보</param>
  /// <param name="setup">검사 설정 정보</param>
  public static KlarfDocument From(
    WaferSurfaceInspectionResult result,
    KlarfInspectionStation       station,
    KlarfInspectionSetup         setup
  ) {
    var defects = result.FrameResults
      .SelectMany(f => f.Defects)
      .ToList()
      .AsReadOnly();

    return new KlarfDocument(
      FileVersion:      CurrentVersion,
      FileTimestamp:    result.CompletedAt,
      Station:          station,
      Setup:            setup,
      Wafer:            result.Wafer,
      Defects:          defects,
      TotalDefectCount: defects.Count
    );
  }
}
