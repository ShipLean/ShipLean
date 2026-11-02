using ShipLean.LocalEnvironment;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Harness;

[TestFixture]
public class LocalSignInTests
{
    [Test]
    public void LocalSignIn_ShouldMatchTheSeededJoel()
    {
        LocalSignIn.Email.ShouldBe(TestUser.Joel.Email);
        LocalSignIn.Password.ShouldBe(TestUser.Joel.Password);
    }
}
