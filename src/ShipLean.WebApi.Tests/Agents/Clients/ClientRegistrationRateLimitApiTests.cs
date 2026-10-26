using System.Net;
using System.Text;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Features.Agents.Clients;

namespace ShipLean.WebApi.Tests.Agents.Clients;

// Its own fixture, so its host's per-IP budget is not shared with another test's registrations.
[TestFixture]
public class ClientRegistrationRateLimitApiTests : ApiFixture
{
    protected override TestUser SignedInAs => TestUser.Anonymous;

    private async Task<HttpStatusCode> RegisterAsync()
    {
        using var content = new StringContent("""{"client_name":"Bot","redirect_uris":["https://agent.example.test/cb"]}""", Encoding.UTF8, "application/json");
        using var response = await Client.PostAsync("/oauth/register", content);
        return response.StatusCode;
    }

    [Test]
    public async Task Register_WhenOverLimit_ShouldAnswer429()
    {
        for (var i = 0; i < ClientsServices.RegistrationsPerMinutePerIp; i++)
        {
            (await RegisterAsync()).ShouldBe(HttpStatusCode.Created);
        }

        (await RegisterAsync()).ShouldBe(HttpStatusCode.TooManyRequests);
    }
}
