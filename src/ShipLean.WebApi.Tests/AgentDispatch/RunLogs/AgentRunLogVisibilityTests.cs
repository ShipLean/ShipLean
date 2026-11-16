using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using ShipLean.Contracts.Usage;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Logging;
using ShipLean.TestSupport.Secrets;
using ShipLean.TestSupport.Usage;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.AgentDispatch.RunLogs;

// D5 and D10: a session's run log is an item attachment that only project admins can list, read over MCP or download; to a member it is
// not there. C11: the agent-log link's token reaches no log line, trace attribute, usage event, panel JSON or get_work_item result.
// Joel is the project's admin and Priya a member. Not parallel: the trace capture needs the OTLP endpoint through the process environment.
[TestFixture]
[NonParallelizable]
public class AgentRunLogVisibilityTests : McpFixture
{
    private const string EndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";
    private readonly RecordedLogs _logs = new();
    private readonly List<string> _spanAttributes = [];
    private readonly List<IDisposable> _listeners = [];
    private string? _previousEndpoint;

    protected override bool WithProbeTools => false;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        base.ConfigureTestServices?.Invoke(services);
        services.RemoveAll<ILoggerFactory>();
        services.AddSingleton<ILoggerFactory>(_logs);
    };

    protected override Task BeforeHostStartAsync()
    {
        _previousEndpoint = Environment.GetEnvironmentVariable(EndpointKey);
        Environment.SetEnvironmentVariable(EndpointKey, "http://127.0.0.1:1");
        var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                lock (_spanAttributes)
                {
                    _spanAttributes.AddRange(SecretLeakAssert.Attributes(activity));
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        _listeners.Add(listener);
        return Task.CompletedTask;
    }

    protected override Task AfterHostStopAsync()
    {
        Environment.SetEnvironmentVariable(EndpointKey, _previousEndpoint);
        return Task.CompletedTask;
    }

    [OneTimeTearDown]
    public void StopListening() => _listeners.ForEach(listener => listener.Dispose());

    private async Task<(AgentRunLogArrange Arranged, Guid RunLogId, string Name)> UploadedAsync()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentRunLogArrange.StartAsync(database, Api.ActingAs(TestUser.Joel));
        using var upload = await Client.SendAsync(AgentRunLogArrange.Upload(arranged.PathAndQuery, AgentRunLogArrange.Text("relay-host.internal:8443 said no\n")));
        upload.StatusCode.ShouldBe(HttpStatusCode.OK, await upload.Content.ReadAsStringAsync());
        await arranged.EndAsFailedAsync(database);
        return (arranged, (await arranged.RunLogIdAsync(database))!.Value, $"agent-run-{arranged.SessionId}.log");
    }

    private async Task<List<Guid>> ListedAsync(TestUser user, Guid itemId)
    {
        using var response = await Api.ActingAs(user).GetAsync($"api/work-items/{itemId}/attachments");
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return [.. JsonDocument.Parse(text).RootElement.EnumerateArray().Select(a => a.GetProperty("id").GetGuid())];
    }

    private async Task<List<string>> McpListedAsync(TestUser user, string key)
    {
        var reply = await CallToolAsync(await TokenAsync(user, ReadOnly), "get_work_item", $$"""{"key":"{{key}}"}""");
        reply.IsToolError().ShouldBeFalse(reply.Body);
        return [.. reply.Structured().GetProperty("attachments").EnumerateArray().Select(a => a.GetProperty("name").GetString()!)];
    }

    private async Task<McpReply> ReadAsync(TestUser user, string key, string name)
        => await CallToolAsync(await TokenAsync(user, ReadOnly), "read_attachment", $$"""{"item":"{{key}}","name":"{{name}}"}""");

    // Mutation: drop the filter from the item's attachment list.
    [Test]
    public async Task RunLog_WhenMember_ShouldNotBeListedReadOrDownloaded()
    {
        var (arranged, runLogId, name) = await UploadedAsync();

        var listed = await ListedAsync(TestUser.Priya, arranged.ItemId);
        var mcpListed = await McpListedAsync(TestUser.Priya, arranged.ItemKey);
        var read = await ReadAsync(TestUser.Priya, arranged.ItemKey, name);
        using var download = await Api.ActingAs(TestUser.Priya).GetAsync($"api/attachments/{runLogId}/content");

        listed.ShouldNotContain(runLogId);
        mcpListed.ShouldNotContain(name);
        read.IsToolError().ShouldBeTrue();
        read.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe("not_found");
        download.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await download.Content.ReadAsStringAsync()).ShouldNotContain("relay-host");
        (await arranged.PanelSessionAsync(Api, TestUser.Priya)).RunLog.ShouldBeNull();
    }

    [Test]
    public async Task RunLog_WhenAdmin_ShouldBeListedReadAndDownloaded()
    {
        var (arranged, runLogId, name) = await UploadedAsync();

        var listed = await ListedAsync(TestUser.Joel, arranged.ItemId);
        var mcpListed = await McpListedAsync(TestUser.Joel, arranged.ItemKey);
        var read = await ReadAsync(TestUser.Joel, arranged.ItemKey, name);
        using var download = await Api.ActingAs(TestUser.Joel).GetAsync($"api/attachments/{runLogId}/content");

        listed.ShouldContain(runLogId);
        mcpListed.ShouldContain(name);
        read.IsToolError().ShouldBeFalse(read.Body);
        read.Structured().GetProperty("name").GetString().ShouldBe(name);
        download.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await download.Content.ReadAsStringAsync()).ShouldBe("relay-host.internal:8443 said no\n");
    }

    [Test]
    public async Task OtherAttachments_WhenMember_ShouldStillBeListed()
    {
        var (arranged, _, _) = await UploadedAsync();
        using var content = new MultipartFormDataContent { { new StringContent("notes"), "file", "notes.txt" } };
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/work-items/{arranged.ItemId}/attachments") { Content = content };
        request.Headers.Add(TestAuthentication.UserHeader, TestUser.Priya.Id.ToString());
        using var added = await Client.SendAsync(request);
        var text = await added.Content.ReadAsStringAsync();
        added.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        var notesId = JsonDocument.Parse(text).RootElement.GetProperty("id").GetGuid();

        (await ListedAsync(TestUser.Priya, arranged.ItemId)).ShouldBe([notesId]);
    }

    // Mutation: log the upload URL at Information when the session starts.
    [Test]
    public async Task Token_ShouldReachNoLogTraceUsageEventPanelOrWorkItemRead()
    {
        var (arranged, _, _) = await UploadedAsync();
        var leak = new SecretLeakAssert(arranged.Token, arranged.Url);

        using var panel = await Api.ActingAs(TestUser.Joel).GetAsync($"api/work-items/{arranged.ItemId}/agent-sessions");
        var panelJson = await panel.Content.ReadAsStringAsync();
        var workItem = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "get_work_item", $$"""{"key":"{{arranged.ItemKey}}"}""");

        panel.StatusCode.ShouldBe(HttpStatusCode.OK);
        workItem.IsToolError().ShouldBeFalse();
        _logs.Events.ShouldNotBeEmpty("the recorder stands in for the host's logger");
        leak.NotInLogs(_logs);
        lock (_spanAttributes)
        {
            _spanAttributes.ShouldContain(attribute => attribute.Contains("/api/uploads/agent-log", StringComparison.Ordinal), "the listener saw the upload span");
            leak.NotInTraceAttributes(_spanAttributes);
        }

        leak.NotInUsageEvents(((RecordingUsageEvents)Host.Services.GetRequiredService<IUsageEvents>()).Events);
        leak.NotInJson(panelJson);
        leak.NotInMcpResult(workItem.Json());
    }
}
