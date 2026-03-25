namespace Core.SharedMemory.Tests.Layout;

[TestFixture]
public class RingBufferLayoutTests
{
    [Test]
    public void HeaderOffset_Is0()    => Assert.That(RingBufferLayout.HeaderOffset,   Is.EqualTo(0));

    [Test]
    public void SlotsOffset_Is64()    => Assert.That(RingBufferLayout.SlotsOffset,    Is.EqualTo(64));

    [Test]
    public void SlotHeaderSize_Is64() => Assert.That(RingBufferLayout.SlotHeaderSize, Is.EqualTo(64));
}
