using System.Runtime.InteropServices;

namespace Core.SharedMemory;

/// <summary>
/// 링버퍼 메모리 레이아웃 상수
///
/// [ Offset 0   ] RingBufferHeader  (64 bytes, cache-line)
/// [ Offset 64  ] Slot[0]           SlotHeader(64) + PixelData
/// [ Offset 64 + slotTotalSize ] Slot[1] ...
/// </summary>
internal static class RingBufferLayout
{
    public const int HeaderOffset   = 0;
    public const int SlotsOffset    = 64;   // header 직후, cache-line 정렬
    public const int SlotHeaderSize = 64;   // cache-line 크기로 정렬
}

internal static class SlotState
{
    public const int Empty   = 0;  // 쓰기 가능 (또는 컨슈머가 읽은 후)
    public const int Writing = 1;  // 프로듀서가 기록 중
    public const int Ready   = 2;  // 컨슈머가 읽기 가능 (OverwriteOldest: 프로듀서도 덮어쓸 수 있음)
}

/// <summary>
/// 링버퍼 전체 메타데이터 (Offset 0, 64 bytes)
/// 초기화 시 1회 기록, 이후 읽기 전용
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 64)]
internal struct RingBufferHeader
{
    [FieldOffset( 0)] public int Capacity;         // 슬롯 총 개수
    [FieldOffset( 4)] public int SlotDataSize;     // 슬롯당 픽셀 데이터 바이트 수
    [FieldOffset( 8)] public int Width;
    [FieldOffset(12)] public int Height;
    [FieldOffset(16)] public int Stride;
    [FieldOffset(20)] public int PixelFormatValue; // Core.Enums.PixelFormat as int
    // [24..63] reserved
}

/// <summary>
/// 슬롯별 메타데이터 (각 슬롯 앞 64 bytes)
/// State 필드는 Volatile/Interlocked로만 접근
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 64)]
internal struct SlotHeader
{
    [FieldOffset( 0)] public long Sequence;     // 단조 증가 시퀀스 번호 (컨슈머 누락 감지용)
    [FieldOffset( 8)] public long TimestampUs;  // epoch microseconds
    [FieldOffset(16)] public int  State;        // SlotState
    [FieldOffset(20)] public int  Pad;
    // [24..63] reserved
}
