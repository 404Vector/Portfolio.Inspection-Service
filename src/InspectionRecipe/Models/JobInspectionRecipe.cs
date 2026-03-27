using InspectionRecipe.Interfaces;

namespace InspectionRecipe.Models;

/// <summary>
/// 연속 시퀀스 검사 잡 레시피 (StartInspectionJob RPC에 대응).
/// proto3 scalar 타입 전체를 더미 필드로 포함하여 gRPC 매핑 검증에 사용된다.
/// </summary>
public record JobInspectionRecipe(
    // ── IInspectionRecipe ─────────────────────────────────────────────────
    string RecipeName       = "DefaultJobRecipe",
    string Description      = "",

    // ── proto3 scalar 타입 커버 ────────────────────────────────────────────
    int    MaxFrameCount    = 100,          // int32
    long   JobTimeoutUs     = 60_000_000L,  // int64  (60 s in µs)
    float  RejectThreshold  = 0.1f,         // float
    double MinPassRate      = 0.99,         // double
    bool   StopOnFirstFail  = false,        // bool
    string JobTag           = "batch",      // string
    byte[] WaferMap         = default!      // bytes
) : IInspectionRecipe;
