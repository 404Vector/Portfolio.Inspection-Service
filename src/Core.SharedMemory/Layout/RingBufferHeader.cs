using System.Runtime.InteropServices;

namespace Core.SharedMemory;

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
