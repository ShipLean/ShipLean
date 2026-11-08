using ShipLean.Contracts.Usage;

namespace ShipLean.UnitTest.Usage;

// Pins the method values of work_item.moved (K3, docs/analytics.md): the clients send exactly these.
[TestFixture]
public class MoveMethodsTests
{
    [Test]
    public void All_ShouldBeTheFiveMethods()
    {
        MoveMethods.All.Order(StringComparer.Ordinal).ShouldBe(["drag", "keyboard", "pick-up", "sheet", "touch-drag"]);
    }
}
