using Core.SharedMemory.Layout;

namespace Core.SharedMemory.Tests.Layout;

[TestFixture]
public class SlotStateTests
{
    [Test]
    public void Empty_Is0()   => Assert.That(SlotState.Empty,   Is.EqualTo(0));

    [Test]
    public void Writing_Is1() => Assert.That(SlotState.Writing, Is.EqualTo(1));

    [Test]
    public void Ready_Is2()   => Assert.That(SlotState.Ready,   Is.EqualTo(2));
}
