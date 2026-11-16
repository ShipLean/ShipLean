using Dapper;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.AgentDispatch.RunLogs;

// The agent-log link is built from instance_settings.base_url only. The Host header of the Start agent request is the caller's to choose
// (AllowedHosts is *), so a link built from it would send the admin-only run log, and a live token, to whatever host a member names.
// With no base_url there is no link at all: no token row and no guidance line. Mutation: fall back to the request's origin again.
[TestFixture]
public class AgentRunLogLinkOriginTests : ApiFixture
{
    private const string SpoofedHost = "evil.example";

    private async Task<(Guid ItemId, string Body)> StartWithSpoofedHostAsync(string baseUrl)
    {
        await using var database = await OpenDatabaseAsync();
        await database.ExecuteAsync("UPDATE instance_settings SET base_url = @baseUrl", new { baseUrl });
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        var item = await arranged.CreateItemAsync(Api);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/work-items/{item.Id}/agent-session");
        request.Headers.Host = SpoofedHost;
        request.Headers.Add(TestAuthentication.UserHeader, TestUser.Joel.Id.ToString());
        using var started = await Client.SendAsync(request);
        started.IsSuccessStatusCode.ShouldBeTrue(await started.Content.ReadAsStringAsync());
        var delivery = (await AgentDispatchArrange.DeliveriesAsync(database, item.Id)).Single(d => d.Action == "created");
        return (item.Id, delivery.Body);
    }

    [Test]
    public async Task Start_WhenBaseUrlIsEmptyAndTheHostIsSpoofed_ShouldIssueNoAgentLogLink()
    {
        var (itemId, body) = await StartWithSpoofedHostAsync(string.Empty);

        await using var database = await OpenDatabaseAsync();
        (await database.ExecuteScalarAsync<int>(
            "SELECT count(*)::int FROM transfer_token WHERE work_item_id = @itemId AND kind = 'agentLog'", new { itemId })).ShouldBe(0, "no agentLog token");
        body.ShouldNotContain(AgentRunLogArrange.LinePrefix, Case.Sensitive);
        body.ShouldNotContain("/api/uploads/agent-log", Case.Sensitive);
    }

    [Test]
    public async Task Start_WhenBaseUrlIsSetAndTheHostIsSpoofed_ShouldBuildTheLinkFromTheBaseUrl()
    {
        var (_, body) = await StartWithSpoofedHostAsync("https://shiplean.example.org/");

        var guidance = AgentRunLogArrange.GuidanceOf(new AgentDispatchArrange.DeliveryRow(Guid.Empty, Guid.Empty, 1, "created", body, "pending", 0));
        AgentRunLogArrange.UrlIn(guidance).ShouldStartWith("https://shiplean.example.org/api/uploads/agent-log?token=", Case.Sensitive);
    }
}
