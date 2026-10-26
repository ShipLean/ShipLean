using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ShipLean.Application.Fakes.Import;
using ShipLean.Application.Features.Import.Jobs;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Comments;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Import;
using ShipLean.Contracts.Import.Jira;
using ShipLean.Contracts.Persisted;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.Import.Apply;
using JiraWorld = ShipLean.WebApi.Tests.Import.Mapping.JiraWorld;

namespace ShipLean.WebApi.Tests.Import.Jobs;

// The base of the import run tests: a seed clone with the real runner, an in-memory Jira, a Jira connection owned by EMPT (Joel is its admin), and helpers that drive the
// import through its HTTP routes and wait for a job state by polling with a deadline.
public abstract class ImportJobsFixture : ApiFixture
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    private readonly List<ProjectEvent> _events = [];
    private IDisposable? _subscription;

    protected static readonly Guid Connection = Guid.NewGuid();

    protected Dictionary<string, byte[]> AttachmentBytes { get; } = [];

    protected InMemoryJiraConnections Connections { get; } = new();

    internal RecordingAttachmentStore Attachments { get; } = new();

    protected InMemoryJiraClient Jira { get; private set; } = null!;

    protected abstract IReadOnlyList<JiraIssue> Issues { get; }

    protected virtual IReadOnlyDictionary<string, IReadOnlyList<JiraComment>> Comments => new Dictionary<string, IReadOnlyList<JiraComment>>();

    // The client the app talks to; a test wraps Jira to hold or watch it.
    protected virtual IJiraClient AppJira => Jira;

    // A fixture whose first host must not run imports (a restart test) removes the runner.
    protected virtual bool RunnerEnabled => true;

    protected IReadOnlyList<ProjectEvent> Events
    {
        get
        {
            lock (_events)
            {
                return [.. _events];
            }
        }
    }

    protected override Action<IServiceCollection>? ConfigureTestServices => Pin;

    protected void Pin(IServiceCollection services)
    {
        Jira ??= new InMemoryJiraClient(JiraWorld.Credentials, JiraWorld.Data(Issues, comments: Comments, attachmentBytes: AttachmentBytes));
        Connections.Arrange(Connection, JiraWorld.Credentials);
        if (!RunnerEnabled)
        {
            services.Remove(services.Single(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(ImportRunner)));
        }

        services.AddSingleton(AppJira);
        services.AddSingleton<IJiraConnections>(Connections);
        services.AddSingleton<ICommentStore>(new RecordingCommentStore());
        services.AddSingleton<IAttachmentStore>(Attachments);
    }

    [OneTimeSetUp]
    public async Task ArrangeConnection()
    {
        await using var database = await OpenDatabaseAsync();
        await database.ExecuteAsync(
            "INSERT INTO jira_connection (id, project_id, base_url, email, created_by) VALUES (@Connection, @project, @url, @email, @joel)",
            new { Connection, project = SeedIds.Projects.Empt, url = JiraWorld.Credentials.BaseUrl, email = JiraWorld.Credentials.Email, joel = SeedIds.Users.Joel });
        _subscription = Host.Services.GetRequiredService<IProjectEvents>().Subscribe(null, e =>
        {
            lock (_events)
            {
                _events.Add(e);
            }

            return Task.CompletedTask;
        });
    }

    [OneTimeTearDown]
    public void StopListening() => _subscription?.Dispose();

    // A project Joel administers, to own a Jira connection (one current connection per project).
    protected async Task<Guid> NewProjectAsync(string key)
    {
        await using var scope = Host.Services.CreateAsyncScope();
        var created = await scope.ServiceProvider.GetRequiredService<ShipLean.Contracts.Projects.IProjectFactory>()
            .CreateAsync(new ShipLean.Contracts.Projects.ProjectCreate($"Owner {key}", key, null, [], [1, 2, 3, 4, 5], null));
        return created.Id;
    }

    protected async Task<ImportMappingDto> SuggestAsync(Guid? previousJobId = null)
        => await ReadAsync<ImportMappingDto>(await Api.PostJsonAsync("/api/import/mapping", new { connectionId = Connection, jiraProjectKey = JiraWorld.ProjectKey, previousJobId }));

    protected async Task<ImportJobDto> StartAsync(string key, ImportMappingDto? mapping = null, ApiClient? api = null, Guid? connection = null)
    {
        var request = new ImportStartRequest(
            connection ?? Connection, JiraWorld.ProjectKey, new ImportTarget(ImportTargetKind.NewProject, key, $"Project {key}", null), mapping ?? await SuggestAsync());
        return await ReadAsync<ImportJobDto>(await (api ?? Api).PostJsonAsync("/api/import/jobs", Body(request)));
    }

    protected Task<ImportJobDto> GetJobAsync(Guid jobId) => GetAsync<ImportJobDto>($"/api/import/jobs/{jobId}");

    protected Task<IReadOnlyList<ImportErrorDto>> ErrorsAsync(Guid jobId) => GetAsync<IReadOnlyList<ImportErrorDto>>($"/api/import/jobs/{jobId}/errors");

    protected Task<IReadOnlyList<ImportLogLineDto>> LogAsync(Guid jobId) => GetAsync<IReadOnlyList<ImportLogLineDto>>($"/api/import/jobs/{jobId}/log");

    protected async Task<T> GetAsync<T>(string path) => await ReadAsync<T>(await Api.GetAsync(path));

    protected async Task<ImportJobDto> PostJobAsync(string path, object body) => await ReadAsync<ImportJobDto>(await Api.PostJsonAsync(path, Body(body)));

    // A body as the SPA writes it: enums as camelCase strings, which the server requires.
    protected static System.Text.Json.Nodes.JsonNode? Body(object value) => JsonSerializer.SerializeToNode(value, Wire);

    protected static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        using (response)
        {
            var text = await response.Content.ReadAsStringAsync();
            response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
            return JsonSerializer.Deserialize<T>(text, Wire)!;
        }
    }

    // Polls the job over the API until the condition holds, or fails with the last state it saw.
    protected async Task<ImportJobDto> WaitForAsync(Guid jobId, Func<ImportJobDto, bool> condition, ApiClient? api = null)
    {
        using var deadline = new CancellationTokenSource(Patience);
        ImportJobDto? last = null;
        try
        {
            while (true)
            {
                using var response = await (api ?? Api).GetAsync($"/api/import/jobs/{jobId}");
                last = JsonSerializer.Deserialize<ImportJobDto>(await response.Content.ReadAsStringAsync(deadline.Token), Wire);
                if (last is not null && condition(last))
                {
                    return last;
                }

                await Task.Delay(20, deadline.Token);
            }
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException($"The import job never reached the state; it was {last?.Status} at {last?.Progress.Percent}%: {last?.Progress.Message}");
        }
    }

    protected Task<ImportJobDto> WaitForAsync(Guid jobId, ImportJobState state) => WaitForAsync(jobId, j => j.Status == state);

    protected async Task<T> QueryAsync<T>(string sql, object? args = null)
    {
        await using var database = await OpenDatabaseAsync();
        return await database.ExecuteScalarAsync<T>(sql, args) ?? throw new InvalidOperationException("The query returned no row.");
    }

    protected async Task<IReadOnlyList<T>> QueryManyAsync<T>(string sql, object? args = null)
    {
        await using var database = await OpenDatabaseAsync();
        return [.. await database.QueryAsync<T>(sql, args)];
    }
}
