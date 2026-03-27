using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using Core.SharedMemory.Enums;
using Core.SharedMemory.Layout;
using Core.SharedMemory.Models;

namespace Core.SharedMemory.Reader;

/// <summary>
/// SharedMemoryRingBuffer(Writer)가 생성한 MMF를 읽는 컨슈머.
///
/// FrameInfo(gRPC로 수신)를 키로 슬롯 주소를 계산하고,
/// Sequence 검증으로 OverwriteOldest 정책에서의 덮어쓰기를 감지한다.
///
/// 슬롯 간격은 MMF 헤더의 SlotDataSize(오버사이즈 고정값)를 기준으로 계산한다.
/// 실제 프레임 크기는 FrameInfo.SizeBytes를 사용한다.
/// </summary>
public sealed unsafe class SharedMemoryRingBufferReader : IDisposable
{
    private readonly MemoryMappedFile         _mmf;
    private readonly MemoryMappedViewAccessor _accessor;
    private readonly byte*                    _base;
    private readonly int                      _slotTotalSize;

    // ── 생성 / 소멸 ──────────────────────────────────────────────────────────

    /// <param name="name">
    /// FrameInfo.SharedMemoryKey — Writer가 생성한 MMF 파일명.
    /// </param>
    public SharedMemoryRingBufferReader(string name)
    {
        string filePath = Path.Combine(Path.GetTempPath(), name);

        _mmf      = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open);
        _accessor = _mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);

        byte* ptr = null;
        _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
        _base = ptr;

        ref var hdr    = ref Unsafe.AsRef<RingBufferHeader>(_base + RingBufferLayout.HeaderOffset);
        _slotTotalSize = RingBufferLayout.SlotHeaderSize + hdr.SlotDataSize;
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// FrameInfo가 가리키는 슬롯의 픽셀 데이터를 <paramref name="dest"/>에 복사한다.
    /// </summary>
    /// <param name="info">Writer로부터 gRPC로 수신한 프레임 메타데이터.</param>
    /// <param name="dest">픽셀 데이터를 받을 버퍼. 크기는 info.SizeBytes 이상이어야 한다.</param>
    public ReadResult TryRead(FrameInfo info, Span<byte> dest)
    {
        ref SlotHeader slot = ref GetSlotHeader(info.SlotIndex);

        // ① 읽기 전 Sequence 스냅샷 — 이후 변경되면 덮어쓰기로 판정
        long seqBefore = Volatile.Read(ref slot.Sequence);

        // ② 요청한 프레임이 이미 덮어써졌는지 확인
        if (seqBefore != info.Sequence)
            return ReadResult.Overwritten;

        // ③ 슬롯이 읽기 가능한 상태인지 확인
        if (Volatile.Read(ref slot.State) != SlotState.Ready)
            return ReadResult.NotReady;

        // ④ 픽셀 데이터 복사 (실제 프레임 크기만큼만)
        new ReadOnlySpan<byte>(SlotDataPtr(info.SlotIndex), (int)info.SizeBytes).CopyTo(dest);

        // ⑤ 복사 도중 덮어써졌는지 확인
        long seqAfter = Volatile.Read(ref slot.Sequence);
        if (seqAfter != seqBefore)
            return ReadResult.Overwritten;

        return ReadResult.Ok;
    }

    // ── 포인터 헬퍼 ──────────────────────────────────────────────────────────

    private ref SlotHeader GetSlotHeader(int slotIndex)
    {
        byte* ptr = _base
                  + RingBufferLayout.SlotsOffset
                  + (long)slotIndex * _slotTotalSize;
        return ref Unsafe.AsRef<SlotHeader>(ptr);
    }

    private byte* SlotDataPtr(int slotIndex) =>
        _base
        + RingBufferLayout.SlotsOffset
        + (long)slotIndex * _slotTotalSize
        + RingBufferLayout.SlotHeaderSize;

    // ── IDisposable ──────────────────────────────────────────────────────────

    public void Dispose()
    {
        _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
        _accessor.Dispose();
        _mmf.Dispose();
    }
}
