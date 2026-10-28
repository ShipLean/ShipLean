using System.Text;
using ShipLean.Application.Features.AgentDispatch;

namespace ShipLean.WebApi.Tests.AgentDispatch;

[TestFixture]
public class LinearSignatureTests
{
    [Test]
    public void Sign_WhenGivenAKnownKeyAndBody_ShouldBeTheLowercaseHexHmacSha256()
    {
        var signature = LinearSignature.Sign("key", Encoding.UTF8.GetBytes("The quick brown fox jumps over the lazy dog"));

        signature.ShouldBe("f7bc83f430538424b13298e6aa6fb143ef4d59a14946175997479dbc2d1a3cd8");
    }
}
