using ShipLean.WebApi.Features.Auth.Proxy;

namespace ShipLean.WebApi.Tests.Auth.Proxy;

// Which jwks_uri a discovery document may name. A remote issuer can't send this host to its own loopback ports.
[TestFixture]
public class ProxyDiscoveredJwksUrlTests
{
    [TestCase("https://idp.example.test", "https://keys.example.test/jwks", true)]
    [TestCase("http://127.0.0.1:8080", "http://127.0.0.1:8080/jwks", true)]
    [TestCase("http://localhost:8080", "http://127.0.0.1:9000/jwks", true)]
    [TestCase("https://idp.example.test", "http://127.0.0.1:6379/jwks", false)]
    [TestCase("https://idp.example.test", "https://localhost/jwks", false)]
    [TestCase("https://idp.example.test", "http://keys.example.test/jwks", false)]
    [TestCase("http://127.0.0.1:8080", "http://keys.example.test/jwks", false)]
    [TestCase("https://idp.example.test", "https://user:pw@keys.example.test/jwks", false)]
    public void IsDiscoveredJwksUrl_WhenChecked_ShouldFollowTheLoopbackRule(string issuer, string jwksUri, bool allowed)
    {
        ProxyKeySource.IsDiscoveredJwksUrl(issuer, jwksUri).ShouldBe(allowed);
    }
}
