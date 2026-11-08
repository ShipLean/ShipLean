using ShipLean.Contracts.Usage;

namespace ShipLean.UnitTest.Usage;

// Pins the method values of auth.signed_in (K3, docs/analytics.md).
[TestFixture]
public class SignInMethodsTests
{
    [Test]
    public void All_ShouldBeTheThreeMethods()
    {
        SignInMethods.All.Order(StringComparer.Ordinal).ShouldBe(["google", "password", "proxy"]);
    }
}
