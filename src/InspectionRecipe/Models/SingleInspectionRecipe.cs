using InspectionRecipe.Interfaces;

namespace InspectionRecipe.Models;

/// <summary>
/// 단일 프레임 검사 레시피 (InspectFrame RPC에 대응).
/// proto3 scalar 타입 전체를 더미 필드로 포함하여 gRPC 매핑 검증에 사용된다.
/// </summary>
public record SingleInspectionRecipe(
    // ── IInspectionRecipe ─────────────────────────────────────────────────
    string RecipeName    = "DefaultSingleRecipe",
    string Description   = "",

    // ── proto3 scalar 타입 커버 ────────────────────────────────────────────
    int    FrameWidth    = 1280,          // int32
    long   TimestampUs   = 0L,            // int64
    float  Threshold     = 0.5f,          // float
    double ConfidenceMin = 0.95,          // double
    bool   SaveOnFail    = false,         // bool
    string AlgorithmTag  = "rule-based",  // string
    byte[] RoiMask       = default!       // bytes
) : IInspectionRecipe;
