namespace Core.SharedMemory.Tests.Layout;

[TestFixture]
public unsafe class SlotHeaderTests
{
    [Test]
    public void Size_Is64Bytes() => Assert.That(sizeof(SlotHeader), Is.EqualTo(64));
}
