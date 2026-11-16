using System.Net;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.AgentDispatch.Routes;

// C10: until the real upload lands the route fails closed: no token is ever valid, signed in or not.
[TestFixture]
public class AgentLogUploadApiTests : ApiFixture
{
    protected override bool UseTestSignIn => false;

    [TestCase("")]
    [TestCase("?token=")]
    [TestCase("?token=shl_not-a-live-token")]
    public async Task Upload_WhenAnyToken_ShouldBeNotFound(string query)
    {
        using var content = new StringContent("last 200 lines", System.Text.Encoding.UTF8, "text/plain");

        using var response = await Client.PostAsync($"api/uploads/agent-log{query}", content);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).ShouldContain("not_found");
    }
}
