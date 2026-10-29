using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Infrastructure;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Attach;

// With no base_url the upload URL must be the host the request came in on, not the service DNS the call was dialed on.
[TestFixture]
public class AttachUploadHostTests : McpFixture
{
    private Guid _feature;
    private string _featureKey = string.Empty;
    private string _baseUrl = string.Empty;

    protected override bool WithProbeTools => false;

    [OneTimeSetUp]
    public async Task ArrangeItem()
    {
        await using var connection = await OpenDatabaseAsync();
        _baseUrl = await connection.QuerySingleAsync<string>("SELECT base_url FROM instance_settings");
        _feature = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithTitle("Upload host").InsertAsync(connection);
        _featureKey = await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @feature", new { feature = _feature });
    }

    [TearDown]
    public async Task RestoreBaseUrl() => await SetBaseUrlAsync(_baseUrl);

    [Test]
    public async Task AttachFile_WhenBaseUrlBlank_ShouldUseTheHostTheRequestCameInOn()
    {
        // The token is bound to the host it was issued for. Issue it for the public host, then clear base_url so the
        // call has to learn that host from the request.
        await SetBaseUrlAsync("https://shiplean.agoda.is");
        var token = await TokenAsync(TestUser.Joel, ReadWrite);
        await SetBaseUrlAsync("");

        var reply = await CallToolAsync(token, "attach_file", $$"""{"feature":"{{_featureKey}}","name":"host.png","content_type":"image/png"}""", request =>
        {
            request.Headers.Add("X-Forwarded-Host", "shiplean.agoda.is");
            request.Headers.Add("X-Forwarded-Proto", "https");
        });

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        new Uri(reply.Structured().GetProperty("upload_url").GetString()!).GetLeftPart(UriPartial.Authority).ShouldBe("https://shiplean.agoda.is");
    }

    [Test]
    public async Task AttachFile_WhenDialedHostIsClusterService_ShouldRefuseTheUploadUrl()
    {
        // The token is bound to the host it was issued for. Issue it for the service name, then clear base_url so the
        // call has only that name, which a laptop cannot open.
        const string service = "http://non-prod.shiplean.svc:8080";
        await SetBaseUrlAsync(service);
        var token = await TokenAsync(TestUser.Joel, ReadWrite);
        await SetBaseUrlAsync("");

        var reply = await CallToolAsync(token, "attach_file", $$"""{"feature":"{{_featureKey}}","name":"cluster.png","content_type":"image/png"}""", request =>
        {
            request.Headers.Host = "non-prod.shiplean.svc:8080";
        });

        reply.IsToolError().ShouldBeTrue();
        var error = reply.Structured().GetProperty("error");
        error.GetProperty("field").GetString().ShouldBe("base64");
        var message = error.GetProperty("message").GetString()!;
        message.ShouldContain("base_url");
        message.ShouldContain("base64");
        await using var connection = await OpenDatabaseAsync();
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM transfer_token WHERE name = 'cluster.png'")).ShouldBe(0);
        (await Client.GetFromJsonAsync<JsonElement>("/health")).GetProperty("publicOrigin").GetString().ShouldBe(PublicOrigin.Unknown);
    }

    private async Task SetBaseUrlAsync(string baseUrl)
    {
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync("UPDATE instance_settings SET base_url = @baseUrl", new { baseUrl });
    }
}
