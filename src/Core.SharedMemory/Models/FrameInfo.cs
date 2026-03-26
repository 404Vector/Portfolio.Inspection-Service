using Core.Enums;

namespace Core.SharedMemory.Models;

/// <summary>
/// 프레임이 링버퍼에 기록된 직후 발행되는 도메인 이벤트 페이로드.
/// gRPC FrameHandle 변환의 원본 데이터.
/// </summary>
public record FrameInfo(
    string      FrameId,
    int         SlotIndex,
    string      SharedMemoryKey,
    long        TimestampUs,
    int         Width,
    int         Height,
    PixelFormat PixelFormat,
    int         Stride,
    long        SizeBytes,
    long        Sequence);
