using System.Net;
using System.Text;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Payloads;

// The payload store's deadlines on a clock the test moves: the upload URL dies after UploadWindow, an uploaded payload after
// Lifetime, and the next issue clears expired bodies (the row stays). The clock only moves forward, so each test issues its own.
[TestFixture]
public class PayloadStoreClockTests : McpFixture
{
    private static readonly TimeSpan JustPast = TimeSpan.FromSeconds(1);

    private readonly FakeTimeProvider _clock = new(TimeProvider.System.GetUtcNow());

    protected override bool WithProbeTools => false;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        base.ConfigureTestServices!(services);
        services.AddSingleton<TimeProvider>(_clock);
    };

    private IToolPayloads Payloads => Host.Services.GetRequiredService<IToolPayloads>();

    [Test]
    public async Task PayloadUpload_WhenUploadWindowPassed_ShouldReturnNotFound()
    {
        var (payloadId, path) = await IssueAsync();
        _clock.Advance(IToolPayloads.UploadWindow + JustPast);

        using var response = await PutAsync(path);

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
        (await Payloads.FindAsync(payloadId, "capture_work", SeedIds.Users.Joel, null)).ShouldBeNull();
    }

    [Test]
    public async Task PayloadFind_WhenLifetimePassed_ShouldReturnNull()
    {
        var (payloadId, path) = await IssueAsync();
        using var response = await PutAsync(path);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        _clock.Advance(IToolPayloads.Lifetime - JustPast);
        var beforeExpiry = await Payloads.FindAsync(payloadId, "capture_work", SeedIds.Users.Joel, null);
        _clock.Advance(JustPast + JustPast);

        var afterExpiry = await Payloads.FindAsync(payloadId, "capture_work", SeedIds.Users.Joel, null);

        beforeExpiry.ShouldNotBeNull();
        afterExpiry.ShouldBeNull();
    }

    [Test]
    public async Task PayloadIssue_WhenExpiredBodiesExist_ShouldClearThem()
    {
        var (expired, path) = await IssueAsync();
        using var response = await PutAsync(path);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        _clock.Advance(IToolPayloads.Lifetime + JustPast);

        var (live, livePath) = await IssueAsync();

        using var liveResponse = await PutAsync(livePath);
        liveResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        await using var connection = await OpenDatabaseAsync();
        var rows = (await connection.QueryAsync<(string Id, bool HasBody, bool Uploaded)>(
            "SELECT id, body IS NOT NULL, uploaded_at IS NOT NULL FROM tool_payload WHERE id IN (@expired, @live)", new { expired, live })).ToDictionary(r => r.Id);
        rows[expired].ShouldBe((expired, false, true));
        rows[live].ShouldBe((live, true, true));
    }

    private async Task<(string PayloadId, string Path)> IssueAsync()
    {
        var issued = await Payloads.IssueAsync(new NewToolPayload("capture_work", null, SeedIds.Users.Joel, null, "Test Agent"));
        return (issued.PayloadId, "/api/uploads/payloads/" + issued.UploadToken);
    }

    private Task<HttpResponseMessage> PutAsync(string path) => Client.PutAsync(path, new ByteArrayContent(Encoding.UTF8.GetBytes("""{"project":"SHIP"}""")));
}
