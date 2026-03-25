namespace Core.SharedMemory.Tests.Layout;

[TestFixture]
public unsafe class RingBufferHeaderTests
{
    [Test]
    public void Size_Is64Bytes() => Assert.That(sizeof(RingBufferHeader), Is.EqualTo(64));
}
