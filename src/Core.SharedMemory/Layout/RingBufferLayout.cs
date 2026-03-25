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
