namespace Core.Models;

/// <summary>
/// 단일 결함의 물리적 위치·크기·분류 정보입니다.
/// KLARF 1.8 DefectList 레코드 한 행에 대응합니다.
/// </summary>
/// <param name="DefectId">결함 일련번호. KLARF DEFECTID. 프레임 내 0-based.</param>
/// <param name="WaferCoord">웨이퍼 좌표계 기준 결함 위치 (µm). KLARF XWAFER, YWAFER.</param>
/// <param name="DieIdx">결함이 속한 Die 인덱스. KLARF XINDEX, YINDEX.</param>
/// <param name="XRelUm">Die 내 결함 상대 X 좌표 (µm). KLARF XREL.</param>
/// <param name="YRelUm">Die 내 결함 상대 Y 좌표 (µm). KLARF YREL.</param>
/// <param name="XSizeUm">결함 X축 크기 (µm). KLARF XSIZE.</param>
/// <param name="YSizeUm">결함 Y축 크기 (µm). KLARF YSIZE.</param>
/// <param name="ClassNumber">결함 분류 코드. KLARF CLASSNUMBER. 0 = Unclassified.</param>
public record DefectInfo(
  int             DefectId,
  WaferCoordinate WaferCoord,
  DieIndex        DieIdx,
  double          XRelUm,
  double          YRelUm,
  double          XSizeUm,
  double          YSizeUm,
  int             ClassNumber
);
