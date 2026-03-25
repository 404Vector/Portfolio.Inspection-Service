using Core.Enums;

namespace Core.SharedMemory.Tests.Writer;

[TestFixture]
public class SharedMemoryRingBufferTests
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

    // ── FrameInfo 반환값 ─────────────────────────────────────────────────────

    [Test]
    public void Write_ReturnsCorrectFrameInfo()
    {
        using var buffer = new SharedMemoryRingBuffer(Options());
        var pixels    = new byte[16];
        var timestamp = DateTimeOffset.UtcNow;

        var info = buffer.Write("frame-1", pixels, 4, 4, PixelFormat.Mono8, 4, timestamp);

        Assert.That(info.FrameId,         Is.EqualTo("frame-1"));
        Assert.That(info.Width,           Is.EqualTo(4));
        Assert.That(info.Height,          Is.EqualTo(4));
        Assert.That(info.PixelFormat,     Is.EqualTo(PixelFormat.Mono8));
        Assert.That(info.Stride,          Is.EqualTo(4));
        Assert.That(info.SizeBytes,       Is.EqualTo(16));
        Assert.That(info.Sequence,        Is.EqualTo(1));
        Assert.That(info.SlotIndex,       Is.EqualTo(0));
        Assert.That(info.SharedMemoryKey, Is.EqualTo(_name));
    }

    // ── Sequence ─────────────────────────────────────────────────────────────

    [Test]
    public void Write_SequenceMonotonicallyIncreases()
    {
        using var buffer = new SharedMemoryRingBuffer(Options());
        var pixels = new byte[16];

        var sequences = Enumerable.Range(0, 5)
            .Select(i => buffer.Write($"f{i}", pixels, 4, 4, PixelFormat.Mono8, 4, DateTimeOffset.UtcNow).Sequence)
            .ToArray();

        Assert.That(sequences, Is.EqualTo(new long[] { 1, 2, 3, 4, 5 }));
    }

    // ── SlotIndex ────────────────────────────────────────────────────────────

    [Test]
    public void Write_SlotIndexWrapsAtCapacity()
    {
        const int capacity = 3;
        using var buffer = new SharedMemoryRingBuffer(Options(slotCount: capacity));
        var pixels = new byte[16];

        var slots = Enumerable.Range(0, capacity * 2)
            .Select(i => buffer.Write($"f{i}", pixels, 4, 4, PixelFormat.Mono8, 4, DateTimeOffset.UtcNow).SlotIndex)
            .ToArray();

        Assert.That(slots, Is.EqualTo(new[] { 0, 1, 2, 0, 1, 2 }));
    }

    // ── FrameGrabbed 이벤트 ──────────────────────────────────────────────────

    [Test]
    public void Write_FiresFrameGrabbedEvent()
    {
        using var buffer = new SharedMemoryRingBuffer(Options());
        FrameInfo? received = null;
        buffer.FrameGrabbed += info => received = info;

        buffer.Write("f1", new byte[16], 4, 4, PixelFormat.Mono8, 4, DateTimeOffset.UtcNow);

        Assert.That(received, Is.Not.Null);
    }

    [Test]
    public void Write_EventPayloadMatchesReturnValue()
    {
        using var buffer = new SharedMemoryRingBuffer(Options());
        FrameInfo? received = null;
        buffer.FrameGrabbed += info => received = info;

        var returned = buffer.Write("f1", new byte[16], 4, 4, PixelFormat.Mono8, 4, DateTimeOffset.UtcNow);

        Assert.That(received, Is.EqualTo(returned));
    }

    // ── OverwriteOldest ──────────────────────────────────────────────────────

    [Test]
    public void Write_ExceedingCapacityDoesNotThrow()
    {
        using var buffer = new SharedMemoryRingBuffer(Options(slotCount: 2));
        var pixels = new byte[16];

        Assert.DoesNotThrow(() =>
        {
            for (int i = 0; i < 10; i++)
                buffer.Write($"f{i}", pixels, 4, 4, PixelFormat.Mono8, 4, DateTimeOffset.UtcNow);
        });
    }
}
