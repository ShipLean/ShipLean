using System.Net;
using System.Text.Json;
using ShipLean.Contracts.Import;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using Shouldly;

namespace ShipLean.WebApi.Tests.Import.Jira;

// A host that allows local Jira addresses (SHIPLEAN_JIRA_ALLOW_LOCAL_ENDPOINTS), turned on through the host's configuration. The client is the real one with its real connector: nothing is pinned.
[TestFixture]
public sealed class JiraLocalEndpointsTests : ApiFixture
{
    private static readonly string Connection = $"api/projects/{SeedIds.Projects.Ship}/jira/connection";

    [OneTimeSetUp]
    public void AllowLocalEndpoints() => JiraStubHost.AllowLocalEndpoints(Host.Services);

    [Test]
    public async Task Jira_WhenLocalEndpointsAllowed_ShouldSaveALocalAddressAndReportItUnreachable()
    {
        using var saved = await Api.PutJsonAsync(Connection, new { baseUrl = $"http://127.0.0.1:{LoopbackPorts.Closed()}", kind = "server", email = string.Empty, token = "local-token-1234" });
        using var test = await Api.PostJsonAsync(Connection + "/test");

        saved.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = JsonSerializer.Deserialize<JiraTestResultDto>(await test.Content.ReadAsStringAsync(), JsonSerializerOptions.Web)!;
        result.Ok.ShouldBeFalse();
        result.Message.ShouldNotBeNullOrWhiteSpace();
    }
}
