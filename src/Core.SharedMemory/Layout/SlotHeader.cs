using System.Runtime.InteropServices;

namespace Core.SharedMemory;

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
