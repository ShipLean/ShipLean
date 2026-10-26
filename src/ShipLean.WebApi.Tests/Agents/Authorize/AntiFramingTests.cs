using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Agents.Authorize;

[TestFixture]
public sealed class AntiFramingTests : AuthorizeFixtureBase
{
    [TestCase("/api/health")]
    [TestCase("/api/me")]
    [TestCase("/authorize?request=00000000-0000-0000-0000-000000000000")]
    [TestCase("/oauth/authorize")]
    [TestCase("/api/no-such-route")]
    public async Task Responses_ShouldForbidFraming(string path)
    {
        using var response = await Client.GetAsync(path);

        response.Headers.GetValues("X-Frame-Options").ShouldBe(["DENY"]);
        response.Headers.GetValues("Content-Security-Policy").ShouldBe(["frame-ancestors 'none'"]);
    }
}
