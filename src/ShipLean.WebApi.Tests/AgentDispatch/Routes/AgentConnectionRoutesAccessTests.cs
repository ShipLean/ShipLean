using System.Net;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.Instance.AgentConnections;

namespace ShipLean.WebApi.Tests.AgentDispatch.Routes;

// C8: health and Test connection are for instance admins; anyone else is 403 and nothing is sent.
[TestFixture]
public class AgentConnectionRoutesAccessTests : ApiFixture
{
    protected override TestUser SignedInAs => TestUser.Mei;

    [TestCase("GET", "health")]
    [TestCase("POST", "test")]
    public async Task Route_WhenCallerIsNotInstanceAdmin_ShouldBeForbidden(string method, string action)
    {
        var url = $"{AgentConnectionsWire.Route}/{Guid.NewGuid()}/{action}";

        using var response = method == "GET" ? await Api.GetAsync(url) : await Api.PostJsonAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
