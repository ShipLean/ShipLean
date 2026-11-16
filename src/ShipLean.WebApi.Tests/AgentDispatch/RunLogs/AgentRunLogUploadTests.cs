using System.Net;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Usage;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Usage;

namespace ShipLean.WebApi.Tests.AgentDispatch.RunLogs;

// C10 end to end: Start agent issues the session's one-use agent-log link into the `created` guidance; an anonymous text/plain POST to it
// stores agent-run-<session id>.log on the item as the session's run log, spends the token and records one usage event. The clock is fake so
// a test can step past the 24-hour lifetime. Every test starts a session on a project of its own.
[TestFixture]
public class AgentRunLogUploadTests : ApiFixture
{
    private const int MiB = 1024 * 1024;

    // Whole seconds, so a timestamp read back from Postgres (microseconds) compares equal.
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(TimeProvider.System.GetUtcNow().UtcTicks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond, TimeSpan.Zero));

    protected override Action<IServiceCollection>? ConfigureTestServices => services => services.AddSingleton<TimeProvider>(_clock);

    private RecordingUsageEvents Usage => (RecordingUsageEvents)Host.Services.GetRequiredService<IUsageEvents>();

    private async Task<AgentRunLogArrange> StartAsync()
    {
        await using var database = await OpenDatabaseAsync();
        return await AgentRunLogArrange.StartAsync(database, Api);
    }

    private Task<HttpResponseMessage> UploadAsync(AgentRunLogArrange arranged, HttpContent content)
        => Client.SendAsync(AgentRunLogArrange.Upload(arranged.PathAndQuery, content));

    [Test]
    public async Task Start_WhenASessionOpens_ShouldIssueADayLongAgentLogLinkInTheCreatedGuidance()
    {
        var arranged = await StartAsync();
        await using var database = await OpenDatabaseAsync();

        var token = await database.QuerySingleAsync<(string Kind, Guid? SessionId, DateTimeOffset ExpiresAt, string Name, string ContentType)>(
            "SELECT kind, session_id, expires_at, name, content_type FROM transfer_token WHERE work_item_id = @itemId AND kind = 'agentLog'", new { itemId = arranged.ItemId });
        var delivery = (await AgentDispatchArrange.DeliveriesAsync(database, arranged.ItemId)).Single();

        token.ShouldBe(("agentLog", (Guid?)arranged.SessionId, _clock.GetUtcNow().AddHours(24), $"agent-run-{arranged.SessionId}.log", "text/plain"), "one agentLog token for the session");
        arranged.Url.ShouldContain("/api/uploads/agent-log?token=");
        AgentDispatchArrange.BodyOf(delivery)["promptContext"]!.GetValue<string>().ShouldContain(AgentRunLogArrange.LinePrefix + arranged.Url, Case.Sensitive);
    }

    // Mutation: attach without setting the column (the panel's runLog stays null).
    [Test]
    public async Task Upload_WhenValid_ShouldAttachTheLogAsTheSessionsRunLog()
    {
        var arranged = await StartAsync();
        await using var database = await OpenDatabaseAsync();

        using var response = await UploadAsync(arranged, AgentRunLogArrange.Text("error: build failed\nat line 2\n"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        await arranged.EndAsFailedAsync(database);
        var runLog = (await arranged.PanelSessionAsync(Api, TestUser.Joel)).RunLog;
        runLog.ShouldNotBeNull();
        runLog.Name.ShouldBe($"agent-run-{arranged.SessionId}.log");
        runLog.SizeBytes.ShouldBe(30);
        runLog.AttachmentId.ShouldBe((await arranged.RunLogIdAsync(database))!.Value);
        var stored = await database.QuerySingleAsync<(Guid UploadedBy, string AgentName, string ContentType)>(
            "SELECT uploaded_by, agent_name, content_type FROM attachment WHERE id = @id", new { id = runLog.AttachmentId });
        stored.UploadedBy.ShouldBe(TestUser.Joel.Id!.Value, "stored as the session's creator");
        stored.AgentName.ShouldStartWith("Relay ", Case.Sensitive, "stamped with the connection's name");
        stored.ContentType.ShouldBe("text/plain");
    }

    // C12 with the route mapped SelfEmitted: the upload itself records agent_session.run_log_uploaded, once. Mutation: remove the emit.
    [Test]
    public async Task Upload_WhenValid_ShouldRecordExactlyOneUploadedEvent()
    {
        var arranged = await StartAsync();

        using var response = await UploadAsync(arranged, AgentRunLogArrange.Text("boom"));
        using var again = await UploadAsync(arranged, AgentRunLogArrange.Text("boom"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        again.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var uploaded = Usage.Events.Where(e => e.Name == UsageEventNames.AgentSessionRunLogUploaded && e.ProjectId == arranged.ProjectId).ToList();
        uploaded.ShouldHaveSingleItem().Outcome.ShouldBe(UsageOutcome.Ok);
    }

    // Mutation: skip spending the token (a second POST stores again).
    [Test]
    public async Task Upload_WhenUsedTwice_ShouldBeNotFoundTheSecondTime()
    {
        var arranged = await StartAsync();

        using var first = await UploadAsync(arranged, AgentRunLogArrange.Text("first"));
        using var second = await UploadAsync(arranged, AgentRunLogArrange.Text("second"));

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ApiClient.ReadApiErrorAsync(second, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Upload_WhenTheLinkIsOlderThanADay_ShouldBeNotFound()
    {
        var arranged = await StartAsync();
        _clock.Advance(TimeSpan.FromHours(25));

        using var response = await UploadAsync(arranged, AgentRunLogArrange.Text("late"));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
        await using var database = await OpenDatabaseAsync();
        (await arranged.RunLogIdAsync(database)).ShouldBeNull();
    }

    [Test]
    public async Task Upload_WhenNotPlainText_ShouldBeAValidationErrorAndKeepTheLink()
    {
        var arranged = await StartAsync();

        using var json = await UploadAsync(arranged, AgentRunLogArrange.Text("{\"log\":\"x\"}", "application/json"));
        using var plain = await UploadAsync(arranged, AgentRunLogArrange.Text("x"));

        (await ApiClient.ReadApiErrorAsync(json, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
        plain.StatusCode.ShouldBe(HttpStatusCode.OK, "a refused type does not spend the link");
    }

    // The body declares no length (chunked), so a check of the declared length alone lets it through. Mutation: check only the declared length.
    [Test]
    public async Task Upload_WhenTheStreamedBodyIsOverOneMebibyte_ShouldBeAValidationErrorAndGiveTheLinkBack()
    {
        var arranged = await StartAsync();

        using var tooBig = await UploadAsync(arranged, AgentRunLogArrange.Chunked(MiB + 1));
        using var atLimit = await UploadAsync(arranged, AgentRunLogArrange.Chunked(MiB));

        (await ApiClient.ReadApiErrorAsync(tooBig, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
        atLimit.StatusCode.ShouldBe(HttpStatusCode.OK, "the refused upload gave the link back, and exactly 1 MiB is allowed");
    }

    [Test]
    public async Task Upload_WhenTheDeclaredLengthIsOverOneMebibyte_ShouldBeAValidationError()
    {
        var arranged = await StartAsync();

        using var response = await UploadAsync(arranged, AgentRunLogArrange.Text(new string('x', MiB + 1)));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    // Mutation: store the log as text/markdown.
    [Test]
    public async Task Download_WhenAdmin_ShouldServeThePlainTextAsAnAttachment()
    {
        var arranged = await StartAsync();
        using var upload = await UploadAsync(arranged, AgentRunLogArrange.Text("the log"));
        upload.StatusCode.ShouldBe(HttpStatusCode.OK);
        await using var database = await OpenDatabaseAsync();
        var id = (await arranged.RunLogIdAsync(database))!.Value;

        using var response = await Api.GetAsync($"api/attachments/{id}/content");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/plain");
        response.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");
        (await response.Content.ReadAsStringAsync()).ShouldBe("the log");
    }
}
