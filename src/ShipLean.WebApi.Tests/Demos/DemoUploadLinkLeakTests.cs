using System.Diagnostics;
using System.Net;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using ShipLean.Application.Features.AgentDispatch;
using ShipLean.Application.Features.Demos.Runs;
using ShipLean.Contracts.Demos;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Logging;
using ShipLean.TestSupport.Secrets;
using ShipLean.TestSupport.Usage;
using ShipLean.WebApi.Tests.AgentDispatch;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Demos;

// C11: the demo upload link is a secret that lives in the `created` delivery's body. A receiver that fails with an error holding the whole
// request body (the worst a relay or a proxy can do) must still not put the token anywhere a person or an agent can read: not in the
// attempt rows, agent_delivery.last_error, the agent panel, the Demo, get_work_item, the logs, the traces or the usage events.
// The error is scrubbed once in AgentDeliverer.DeliverAsync (AgentErrorText.Scrub); removing that call turns this test red.
// Not parallel: the trace listener sees every request of the process.
[TestFixture]
[NonParallelizable]
public class DemoUploadLinkLeakTests : McpFixture
{
    private static readonly DateTimeOffset Start = new(2026, 11, 16, 4, 0, 0, TimeSpan.Zero);

    private readonly RecordedLogs _logs = new();
    private readonly List<string> _spans = [];
    private ActivityListener? _listener;

    protected override bool WithProbeTools => false;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        base.ConfigureTestServices!(services);
        services.RemoveAll<ISprintDemoData>();
        services.AddScoped<ISprintDemoData, SprintNamedDemoData>();
        services.RemoveAll<ISprintDemoPrompt>();
        services.AddSingleton<ISprintDemoPrompt>(new UploadUrlPrompt());
        services.RemoveAll<ILoggerFactory>();
        services.AddSingleton<ILoggerFactory>(_logs);
    };

    protected override Task BeforeHostStartAsync()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Microsoft.AspNetCore",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                lock (_spans)
                {
                    _spans.AddRange(SecretLeakAssert.Attributes(activity));
                }
            },
        };
        ActivitySource.AddActivityListener(_listener);
        return Task.CompletedTask;
    }

    [OneTimeTearDown]
    public void StopListening() => _listener?.Dispose();

    // A relay that answers every send by failing with its own request body in the message.
    private sealed class EchoingReceiver : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException($"Relay rejected the job: {await request.Content!.ReadAsStringAsync(cancellationToken)}");
    }

    [Test]
    public async Task Demo_WhenTheDeliveryFailsWithItsBodyInTheError_ShouldLeakNoUploadToken()
    {
        await using var database = await OpenDatabaseAsync();
        Guid projectId, sprintId, itemId;
        await using (var scope = Host.Services.CreateAsyncScope())
        {
            var starter = scope.ServiceProvider.GetRequiredService<IDemoRunStarter>();
            (projectId, sprintId, itemId) = await DemoRunArrange.NewDemoAsync(database, Api, starter);
            await starter.StartSessionAsync(DemoRunArrange.Request(projectId, sprintId, itemId), CancellationToken.None);
        }

        var token = DemoRunArrange.TokenIn((await AgentDispatchArrange.DeliveriesAsync(database, itemId)).Single().Body);
        token.ShouldNotBeEmpty();
        await database.ExecuteAsync("UPDATE agent_delivery SET status = 'failed', last_error = 'Set aside by a test.' WHERE status = 'pending' AND session_id NOT IN (SELECT id FROM agent_session WHERE work_item_id = @itemId)", new { itemId });
        await database.ExecuteAsync("UPDATE agent_delivery SET next_attempt_at = @now WHERE session_id IN (SELECT id FROM agent_session WHERE work_item_id = @itemId)", new { now = Start, itemId });

        var clock = new FakeTimeProvider(Start);
        using var relay = new HttpClient(new EchoingReceiver());
        var deliverer = new AgentDeliverer(
            Host.Services.GetRequiredService<NpgsqlDataSource>(),
            Host.Services.GetRequiredService<IAgentConnections>(),
            new StubHttpClientFactory(relay),
            Host.Services.GetRequiredService<AgentSessionNotifier>(),
            Host.Services.GetRequiredService<IAgentDeliveryAttempts>(),
            clock,
            new Logger<AgentDeliverer>(_logs));
        await deliverer.RunDueAsync(CancellationToken.None);
        clock.Advance(TimeSpan.FromMinutes(1));
        await deliverer.RunDueAsync(CancellationToken.None);

        // Not vacuous: the session ran out of tries, both sends are recorded, and each error names the failure.
        (await database.QuerySingleAsync<string>("SELECT state FROM agent_session WHERE work_item_id = @itemId", new { itemId })).ShouldBe("failed");
        var attempts = (await database.QueryAsync<(int Attempt, string? Error)>(
            "SELECT a.attempt, a.error FROM agent_delivery_attempt a JOIN agent_delivery d ON d.id = a.delivery_id JOIN agent_session s ON s.id = d.session_id WHERE s.work_item_id = @itemId ORDER BY a.attempt", new { itemId })).ToList();
        attempts.Select(a => a.Attempt).ShouldBe([1, 2]);
        attempts.ShouldAllBe(a => a.Error != null && a.Error.Contains("HttpRequestException", StringComparison.Ordinal));
        var leak = new SecretLeakAssert(token);
        foreach (var attempt in attempts)
        {
            leak.NotInJson($$"""{"error":{{System.Text.Json.JsonSerializer.Serialize(attempt.Error)}}}""");
        }

        var lastError = await database.QuerySingleAsync<string?>(
            "SELECT d.last_error FROM agent_delivery d JOIN agent_session s ON s.id = d.session_id WHERE s.work_item_id = @itemId", new { itemId });
        lastError.ShouldNotBeNull();
        leak.NotInJson($$"""{"lastError":{{System.Text.Json.JsonSerializer.Serialize(lastError)}}}""");

        using var panel = await Api.ActingAs(TestUser.Joel).GetAsync($"api/work-items/{itemId}/agent-sessions");
        var panelJson = await panel.Content.ReadAsStringAsync();
        panel.StatusCode.ShouldBe(HttpStatusCode.OK, panelJson);
        panelJson.ShouldContain("HttpRequestException");
        leak.NotInJson(panelJson);

        using var demo = await Api.ActingAs(TestUser.Joel).GetAsync($"api/sprints/{sprintId}/demo");
        var demoJson = await demo.Content.ReadAsStringAsync();
        demo.StatusCode.ShouldBe(HttpStatusCode.OK, demoJson);
        leak.NotInJson(demoJson);

        var key = await database.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @itemId", new { itemId });
        var mcp = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "get_work_item", $$"""{"key":"{{key}}"}""");
        mcp.Status.ShouldBe(HttpStatusCode.OK, mcp.Body);
        leak.NotInMcpResult(mcp.Body);

        leak.NotInLogs(_logs);
        _logs.Events.ShouldContain(e => e.Contains("failed", StringComparison.OrdinalIgnoreCase), "the failures were logged, so the log check saw something");
        string[] spans;
        lock (_spans)
        {
            spans = [.. _spans];
        }

        spans.ShouldNotBeEmpty();
        leak.NotInTraceAttributes(spans);
        leak.NotInUsageEvents(((RecordingUsageEvents)Host.Services.GetRequiredService<ShipLean.Contracts.Usage.IUsageEvents>()).Events);
    }
}
