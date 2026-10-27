using Dapper;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
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

    private async Task SetBaseUrlAsync(string baseUrl)
    {
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync("UPDATE instance_settings SET base_url = @baseUrl", new { baseUrl });
    }
}
