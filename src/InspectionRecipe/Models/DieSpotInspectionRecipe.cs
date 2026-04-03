using Core.Models;
using InspectionRecipe.Interfaces;

namespace InspectionRecipe.Models;

/// <summary>
/// 특정 Die 1개를 지목하여 좌·중·우 3-shot으로 die-to-die 검사하는 레시피.
/// InspectFrame RPC에 대응합니다.
/// ScanPlan 없이 지정된 웨이퍼 좌표에서 단발 촬영 후 검사합니다.
/// 검사 대상 웨이퍼는 실행 시점에 별도로 선택됩니다.
/// </summary>
/// <param name="RecipeName">레시피 식별 이름</param>
/// <param name="Description">레시피 설명</param>
/// <param name="Fov">카메라 FOV 크기 (µm)</param>
/// <param name="ShotCenter">촬영 중심 좌표 (웨이퍼 좌표계, µm)</param>
/// <param name="Threshold">검사 알고리즘 판정 임계값 (0.0 ~ 1.0)</param>
/// <param name="SaveOnFail">불량 판정 시 이미지를 저장할지 여부</param>
public record DieSpotInspectionRecipe(
  string          RecipeName,
  string          Description,
  FovSize         Fov,
  WaferCoordinate ShotCenter,
  double          Threshold  = 0.5,
  bool            SaveOnFail = false
) : IInspectionRecipe;
