using Core.Models;
using InspectionRecipe.Interfaces;

namespace InspectionRecipe.Models;

/// <summary>
/// 웨이퍼 전체 Surface를 Boustrophedon 경로로 스캔하여 die-to-die 검사하는 레시피.
/// StartInspectionJob RPC에 대응합니다.
/// DieMap과 ScanPlan은 이 레시피를 입력으로 서비스에서 생성합니다.
/// </summary>
/// <param name="RecipeName">레시피 식별 이름</param>
/// <param name="Description">레시피 설명</param>
/// <param name="WaferId">검사 대상 웨이퍼의 식별자. WaferInfo 테이블에서 조회하여 사용한다.</param>
/// <param name="Fov">카메라 FOV 크기 (µm)</param>
/// <param name="OverlapXum">Shot 간 X축 오버랩 (µm)</param>
/// <param name="OverlapYum">Sector 간 Y축 오버랩 (µm)</param>
/// <param name="StopOnFirstFail">첫 번째 불량 발생 시 잡을 중단할지 여부</param>
/// <param name="MaxFrameCount">최대 촬영 프레임 수. 0이면 제한 없음.</param>
public record WaferSurfaceInspectionRecipe(
  string  RecipeName,
  string  Description,
  string  WaferId,
  FovSize Fov,
  double  OverlapXum      = 0.0,
  double  OverlapYum      = 0.0,
  bool    StopOnFirstFail = false,
  int     MaxFrameCount   = 0
) : IInspectionRecipe;
