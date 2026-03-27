using System.Runtime.InteropServices;

namespace Core.SharedMemory.Layout;

/// <summary>
/// 링버퍼 전체 메타데이터 (Offset 0, 64 bytes)
/// 초기화 시 1회 기록, 이후 읽기 전용
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 64)]
internal struct RingBufferHeader
{
    [FieldOffset(0)] public int Capacity;     // 슬롯 총 개수
    [FieldOffset(4)] public int SlotDataSize; // 슬롯당 최대 픽셀 데이터 바이트 수 (오버사이즈)
    // [8..63] reserved
}
