using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using ShipLean.Application.Features.Demos.Upload;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Usage;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Usage;
using ShipLean.WebApi.Tests.Instance.AgentConnections;
using ShipLean.WebApi.Tests.ProjectSettings.Api;

namespace ShipLean.WebApi.Tests.Demos;

// K8's "Where the URL goes": the upload link's token must reach no log event, no usage row, no trace span and no agent-panel read. Each test sees the
// real token of a link it issued. Not parallel: the trace test needs the OTLP endpoint through the process environment (see TelemetryTracesTests).
[TestFixture]
[NonParallelizable]
public class DemoUrlExposureTests : ApiFixture
{
    private const string EndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";
    private readonly RecordingDemoDelivery _delivery = new();
    private readonly RecordedLogs _logs = new();
    private readonly List<(string Name, string? Query, string? Route, List<string> Values)> _spans = [];
    private readonly SemaphoreSlim _spanStopped = new(0);
    private readonly List<IDisposable> _listeners = [];
    private string? _previousEndpoint;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.RemoveAll<IDemoDelivery>();
        services.AddSingleton(_delivery);
        services.AddScoped<IDemoDelivery, ActingDemoDelivery>();
        services.RemoveAll<ILoggerFactory>();
        services.AddSingleton<ILoggerFactory>(_logs);
    };

    protected override Task BeforeHostStartAsync()
    {
        _previousEndpoint = Environment.GetEnvironmentVariable(EndpointKey);
        Environment.SetEnvironmentVariable(EndpointKey, "http://127.0.0.1:1");
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Microsoft.AspNetCore",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = Record,
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

    // The span as it ends, after the instrumentation has named it: kept as strings, because the Activity is recycled.
    private void Record(Activity activity)
    {
        if (activity.Kind != ActivityKind.Server)
        {
            return;
        }

        lock (_spans)
        {
            _spans.Add((activity.DisplayName, activity.GetTagItem("url.query")?.ToString(), activity.GetTagItem("http.route")?.ToString(),
                [activity.DisplayName, .. activity.TagObjects.Select(tag => $"{tag.Key}={tag.Value}")]));
        }

        _spanStopped.Release();
    }

    [SetUp]
    public void ResetDelivery() => _delivery.Reset();

    private const string Chapters = """{"durationSeconds": 60, "chapters": [{"title": "Intro", "startSeconds": 0}]}""";

    private async Task<DemoUploadArrange.Arranged> NewAsync(Guid? userId = null)
    {
        await using var database = await OpenDatabaseAsync();
        return await DemoUploadArrange.NewAsync(database, Host.Services, userId ?? SeedIds.Users.Joel);
    }

    private Task<HttpResponseMessage> PostAsync(DemoUploadArrange.Arranged upload, string chapters = Chapters)
        => Client.PostAsync(upload.Path, DemoUploadArrange.Form(chapters, "# The demo", DemoUploadArrange.Mp4()));

    private static string[] Forms(string token) => [token, Uri.EscapeDataString(token), WebUtility.UrlEncode(token)];

    [Test]
    public async Task Upload_WhenItFails_ShouldLogNoToken()
    {
        var failing = await NewAsync();
        _delivery.Before = _ => throw new InvalidOperationException("The blob store is down.");
        var badChapters = await NewAsync();
        var expired = await NewAsync();
        await using (var database = await OpenDatabaseAsync())
        {
            await database.ExecuteAsync("UPDATE transfer_token SET expires_at = now() - interval '1 minute' WHERE work_item_id = @itemId", new { itemId = expired.ItemId });
        }

        using var serverError = await PostAsync(failing);
        using var invalid = await PostAsync(badChapters, """{"durationSeconds": 60, "chapters": []}""");
        using var notFound = await PostAsync(expired);

        serverError.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        notFound.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var events = _logs.Events;
        events.ShouldContain(line => line.Contains("blob store is down", StringComparison.Ordinal), "the failure was logged");
        events.ShouldContain(line => line.Contains("RequestPath:/api/uploads/demo", StringComparison.Ordinal), "the recorder sees ASP.NET's request scope");
        foreach (var token in new[] { failing.Token, badChapters.Token, expired.Token }.SelectMany(Forms))
        {
            events.ShouldNotContain(line => line.Contains(token, StringComparison.Ordinal));
        }
    }

    [Test]
    public async Task Usage_ShouldHoldNoToken()
    {
        var upload = await NewAsync();

        using var response = await PostAsync(upload);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var recorded = ((RecordingUsageEvents)Host.Services.GetRequiredService<IUsageEvents>()).Events;
        recorded.ShouldNotBeEmpty();
        var text = JsonSerializer.Serialize(recorded);
        foreach (var token in Forms(upload.Token))
        {
            text.ShouldNotContain(token);
        }
    }

    [Test]
    public async Task Upload_ShouldPutNoTokenInATraceTag()
    {
        var upload = await NewAsync();

        using var response = await PostAsync(upload);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (string Name, string? Query, string? Route, List<string> Values) span;
        while (true)
        {
            await _spanStopped.WaitAsync();
            lock (_spans)
            {
                var found = _spans.Where(s => s.Route == "/api/uploads/demo").ToList();
                if (found.Count > 0)
                {
                    span = found[^1];
                    break;
                }
            }
        }

        span.Query.ShouldBeNull();
        span.Name.ShouldBe("POST /api/uploads/demo");
        foreach (var token in Forms(upload.Token))
        {
            span.Values.ShouldNotContain(value => value.Contains(token, StringComparison.Ordinal));
        }
    }

    [Test]
    public async Task AgentPanel_ShouldShowNoDeliveryBody()
    {
        var upload = await NewAsync();
        await using var database = await OpenDatabaseAsync();
        var itemId = await ArrangeSessionWithLinkInItsDeliveryAsync(database, upload.Token, (SeedIds.Users.Joel, ProjectRole.Admin));

        using var response = await Api.GetAsync($"api/work-items/{itemId}/agent-sessions");

        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        JsonDocument.Parse(text).RootElement.GetProperty("sessions").GetArrayLength().ShouldBe(1);
        foreach (var token in Forms(upload.Token))
        {
            text.ShouldNotContain(token);
        }

        text.ShouldNotContain("/api/uploads/demo");
    }

    [Test]
    public async Task AgentPanel_WhenNotMember_ShouldBeNotFound()
    {
        var upload = await NewAsync();
        await using var database = await OpenDatabaseAsync();
        var itemId = await ArrangeSessionWithLinkInItsDeliveryAsync(database, upload.Token, (SeedIds.Users.Priya, ProjectRole.Admin));

        using var response = await Api.GetAsync($"api/work-items/{itemId}/agent-sessions");

        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound, text);
        foreach (var token in Forms(upload.Token))
        {
            text.ShouldNotContain(token);
        }
    }

    // A project (with the given members only) whose BAU item has a finished session and a delivery whose body is the prompt with the link in it.
    private async Task<Guid> ArrangeSessionWithLinkInItsDeliveryAsync(Npgsql.NpgsqlConnection database, string token, params (Guid UserId, ProjectRole Role)[] members)
    {
        var project = await ArrangedProject.CreateAsync(database, members);
        var itemId = await new WorkItemBuilder(project.Id).WithType("bau").WithTitle("Sprint 23 demo").InsertAsync(database);
        var connectionId = (await AgentConnectionsWire.CreateAsync(Api, $"Relay {Guid.NewGuid():N}")).Connection.Id;
        var sessionId = Guid.CreateVersion7();
        await database.ExecuteAsync(
            """
            INSERT INTO agent_session (id, work_item_id, connection_id, state, repo_url, base_branch, external_urls, plan, last_activity_at, created_by, created_at, ended_at)
            VALUES (@sessionId, @itemId, @connectionId, 'completed', 'https://github.com/ShipLean/ShipLean.git', 'develop', '[]'::jsonb, '[]'::jsonb, now(), @user, now(), now())
            """,
            new { sessionId, itemId, connectionId, user = SeedIds.Users.Joel });
        var body = JsonSerializer.Serialize(new { prompt = $"curl -F video=@demo.mp4 'https://planner.example.com/api/uploads/demo?token={token}'" });
        await database.ExecuteAsync(
            "INSERT INTO agent_delivery (id, session_id, seq, action, body, next_attempt_at, status, created_at) VALUES (@id, @sessionId, 1, 'created', @body::jsonb, now(), 'sent', now())",
            new { id = Guid.CreateVersion7(), sessionId, body });
        return itemId;
    }
}
