using Core.Enums;

namespace Core.SharedMemory.Tests.Reader;

[TestFixture]
public class SharedMemoryRingBufferReaderTests
{
    private string _name = string.Empty;

    [SetUp]
    public void SetUp() => _name = $"test_{Guid.NewGuid():N}";

    [TearDown]
    public void TearDown()
    {
        string path = Path.Combine(Path.GetTempPath(), _name);
        if (File.Exists(path)) File.Delete(path);
    }

    private RingBufferOptions Options(int slotCount = 4) => new()
    {
        Name        = _name,
        Width       = 4,
        Height      = 4,
        SlotCount   = slotCount,
        PixelFormat = PixelFormat.Mono8   // 4*4*1 = 16 bytes
    };

    // ── Ok ───────────────────────────────────────────────────────────────────

    [Test]
    public void TryRead_ReturnsOk_WhenSlotIsReady()
    {
        using var writer = new SharedMemoryRingBuffer(Options());
        var info = writer.Write("f1", new byte[16], 4, 4, PixelFormat.Mono8, 4, DateTimeOffset.UtcNow);

        using var reader = new SharedMemoryRingBufferReader(_name);
        var result = reader.TryRead(info, new byte[info.SizeBytes]);

        Assert.That(result, Is.EqualTo(ReadResult.Ok));
    }

    [Test]
    public void TryRead_PixelDataMatchesWritten()
    {
        var expected = Enumerable.Range(1, 16).Select(i => (byte)i).ToArray();
        using var writer = new SharedMemoryRingBuffer(Options());
        var info = writer.Write("f1", expected, 4, 4, PixelFormat.Mono8, 4, DateTimeOffset.UtcNow);

        using var reader = new SharedMemoryRingBufferReader(_name);
        var dest = new byte[info.SizeBytes];
        reader.TryRead(info, dest);

        Assert.That(dest, Is.EqualTo(expected));
    }

    // ── NotReady ─────────────────────────────────────────────────────────────

    [Test]
    public void TryRead_ReturnsNotReady_WhenSlotNeverWritten()
    {
        // capacity=2 → slot[1]은 초기 상태(Sequence=0, State=Empty)
        using var writer = new SharedMemoryRingBuffer(Options(slotCount: 2));
        writer.Write("f1", new byte[16], 4, 4, PixelFormat.Mono8, 4, DateTimeOffset.UtcNow);

        // Sequence=0: 초기화 값과 일치 → Overwritten 통과 → State=Empty → NotReady
        var unwrittenInfo = new FrameInfo("f", 1, _name, 0, 4, 4, PixelFormat.Mono8, 4, 16, Sequence: 0);

        using var reader = new SharedMemoryRingBufferReader(_name);
        var result = reader.TryRead(unwrittenInfo, new byte[16]);

        Assert.That(result, Is.EqualTo(ReadResult.NotReady));
    }

    // ── Overwritten ──────────────────────────────────────────────────────────

    [Test]
    public void TryRead_ReturnsOverwritten_WhenSlotOverwritten()
    {
        // capacity=1 → 두 번째 Write가 slot[0]을 덮어씀
        using var writer = new SharedMemoryRingBuffer(Options(slotCount: 1));
        var firstInfo = writer.Write("f1", new byte[16], 4, 4, PixelFormat.Mono8, 4, DateTimeOffset.UtcNow);
        writer.Write("f2", new byte[16], 4, 4, PixelFormat.Mono8, 4, DateTimeOffset.UtcNow);

        using var reader = new SharedMemoryRingBufferReader(_name);
        // firstInfo.Sequence=1이지만 슬롯에는 Sequence=2
        var result = reader.TryRead(firstInfo, new byte[16]);

        Assert.That(result, Is.EqualTo(ReadResult.Overwritten));
    }
}
