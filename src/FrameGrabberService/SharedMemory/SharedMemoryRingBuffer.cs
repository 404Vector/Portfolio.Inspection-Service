using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using FrameGrabberService.Grabbers;
using DomainPixelFormat = FrameGrabberService.Grabbers.PixelFormat;

namespace FrameGrabberService.SharedMemory;

/// <summary>
/// 단일 MMF 위에 구현된 고정 크기 링버퍼 (SPSC, OverwriteOldest 정책).
///
/// 프로듀서(FramePumpService)만 Write()를 호출하며,
/// 프레임 기록 완료 시 FrameGrabbed 이벤트를 발행한다.
/// 컨슈머는 FrameInfo의 SlotIndex로 MMF의 해당 슬롯을 직접 읽는다.
/// </summary>
public sealed unsafe class SharedMemoryRingBuffer : IDisposable
{
    private readonly MemoryMappedFile         _mmf;
    private readonly MemoryMappedViewAccessor _accessor;
    private readonly byte*                    _base;

    private readonly int  _capacity;
    private readonly int  _slotDataSize;
    private readonly int  _slotTotalSize;
    private          long _writeSeq;       // 단조 증가, 프로듀서만 접근

    public string Name { get; }

    /// <summary>
    /// 프레임이 링버퍼에 기록되고 Ready 상태로 전환된 직후 발행.
    /// 이벤트 핸들러는 짧게 유지하거나 Channel로 오프로드할 것.
    /// </summary>
    public event Action<FrameInfo>? FrameGrabbed;

    // ── 생성 / 소멸 ──────────────────────────────────────────────────────────

    public SharedMemoryRingBuffer(RingBufferOptions options)
    {
        Name           = options.Name;
        _capacity      = options.SlotCount;
        _slotDataSize  = options.Width * options.Height * BytesPerPixel(options.PixelFormat);
        _slotTotalSize = RingBufferLayout.SlotHeaderSize + _slotDataSize;

        long   totalSize = RingBufferLayout.SlotsOffset + (long)_capacity * _slotTotalSize;
        string filePath  = Path.Combine(Path.GetTempPath(), options.Name);

        // CreateFromFile: Windows/macOS/Linux 모두 지원
        // 컨슈머는 동일 경로로 CreateFromFile(FileMode.Open)하여 접근
        _mmf      = MemoryMappedFile.CreateFromFile(
                        filePath, FileMode.Create, options.Name, totalSize);
        _accessor = _mmf.CreateViewAccessor(0, totalSize, MemoryMappedFileAccess.ReadWrite);

        byte* ptr = null;
        _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
        _base = ptr;

        InitializeHeader(options);
        InitializeSlots();
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// 프레임을 다음 슬롯에 기록하고 FrameGrabbed 이벤트를 발행한다.
    /// 버퍼가 가득 찬 경우 가장 오래된 슬롯을 덮어쓴다(OverwriteOldest).
    /// </summary>
    public FrameInfo Write(GrabbedFrame frame)
    {
        long seq       = ++_writeSeq;
        int  slotIndex = (int)((seq - 1) % _capacity);

        ref SlotHeader slot = ref GetSlotHeader(slotIndex);

        // Writing 으로 전환 (Ready 슬롯도 덮어쓸 수 있음 — OverwriteOldest)
        Volatile.Write(ref slot.State, SlotState.Writing);

        // 픽셀 데이터 복사
        fixed (byte* src = frame.PixelData)
        {
            Buffer.MemoryCopy(src, SlotDataPtr(slotIndex), _slotDataSize, frame.PixelData.Length);
        }

        // 메타데이터 기록
        slot.TimestampUs = frame.Timestamp.ToUnixTimeMilliseconds() * 1_000L;
        slot.Sequence    = seq;

        // Ready 로 전환 (release barrier)
        Volatile.Write(ref slot.State, SlotState.Ready);

        var info = new FrameInfo(
            FrameId:         frame.FrameId,
            SlotIndex:       slotIndex,
            SharedMemoryKey: Name,
            TimestampUs:     slot.TimestampUs,
            Width:           frame.Width,
            Height:          frame.Height,
            PixelFormat:     frame.PixelFormat,
            Stride:          frame.Stride,
            SizeBytes:       _slotDataSize,
            Sequence:        seq);

        FrameGrabbed?.Invoke(info);
        return info;
    }

    // ── 초기화 ───────────────────────────────────────────────────────────────

    private void InitializeHeader(RingBufferOptions options)
    {
        ref var hdr = ref Unsafe.AsRef<RingBufferHeader>(_base + RingBufferLayout.HeaderOffset);
        hdr.Capacity         = _capacity;
        hdr.SlotDataSize     = _slotDataSize;
        hdr.Width            = options.Width;
        hdr.Height           = options.Height;
        hdr.Stride           = options.Width * BytesPerPixel(options.PixelFormat);
        hdr.PixelFormatValue = (int)options.PixelFormat;
    }

    private void InitializeSlots()
    {
        for (int i = 0; i < _capacity; i++)
        {
            ref var slot  = ref GetSlotHeader(i);
            slot.State    = SlotState.Empty;
            slot.Sequence = 0;
        }
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

    private static int BytesPerPixel(DomainPixelFormat fmt) => fmt switch
    {
        DomainPixelFormat.Rgb8 or DomainPixelFormat.Bgr8 => 3,
        _                                                 => 1
    };

    // ── IDisposable ──────────────────────────────────────────────────────────

    public void Dispose()
    {
        _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
        _accessor.Dispose();
        _mmf.Dispose();
    }
}
