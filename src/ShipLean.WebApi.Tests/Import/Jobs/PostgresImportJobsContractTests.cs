using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using ShipLean.Application.Database;
using ShipLean.Application.Features.Import.Jobs;
using ShipLean.Application.Fakes.Import;
using ShipLean.Contracts.Import;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Projects;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Contracts.Import;
using ShipLean.TestSupport.Database;
using ShipLean.TestSupport.Hosting;
using JiraWorld = ShipLean.WebApi.Tests.Import.Mapping.JiraWorld;

namespace ShipLean.WebApi.Tests.Import.Jobs;

// The jobs contract on Postgres: the real app on a seed clone with the runner taken out (a queued job stays queued, as the contract expects) and an in-memory Jira.
// An arranged job targets a fresh project and its errors name Jira issues the in-memory Jira has, so retrying one succeeds.
public sealed class PostgresImportJobsContractTests : ImportJobsContract
{
    private const int FirstIssue = 101;
    private const int IssueCount = 400;

    private ClonedDatabase _database = null!;
    private ShipLeanHost _host = null!;
    private AsyncServiceScope _scope;
    private InMemoryJiraConnections _connections = null!;
    private ImportMappingDto _mapping = null!;
    private int _nextIssue = FirstIssue;
    private int _nextProject;

    protected override IImportJobs Jobs => _scope.ServiceProvider.GetRequiredService<IImportJobs>();

    [OneTimeSetUp]
    public async Task StartHost()
    {
        DapperTypeHandlers.Register();
        _database = await ShipLeanDatabase.CloneSeedAsync();
        _connections = new InMemoryJiraConnections();
        var issues = Enumerable.Range(FirstIssue, IssueCount).Select(n => JiraWorld.Issue($"PLAT-{n}")).ToList();
        var jira = new InMemoryJiraClient(JiraWorld.Credentials, JiraWorld.Data(issues));
        _host = ShipLeanHost.Start(_database.ConnectionString, TestUser.Joel, services =>
        {
            services.Remove(services.Single(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(ImportRunner)));
            services.AddSingleton<IJiraClient>(jira);
            services.AddSingleton<IJiraConnections>(_connections);
        });
        _host.ActAs(SeedIds.Users.Joel);
        _scope = _host.Services.CreateAsyncScope();
        var connectionId = await ArrangeConnectionAsync();
        _mapping = await _scope.ServiceProvider.GetRequiredService<IImportMappings>().SuggestAsync(connectionId, JiraWorld.ProjectKey, null);
    }

    [OneTimeTearDown]
    public async Task StopHost()
    {
        await _scope.DisposeAsync();
        await _host.DisposeAsync();
        await _database.DisposeAsync();
    }

    protected override async Task<Guid> ArrangeConnectionAsync()
    {
        // One current connection per project, so each arranged connection belongs to a project of its own.
        var id = Guid.NewGuid();
        var projectId = await NewEmptyProjectAsync();
        await using var connection = await _database.OpenAsync();
        await connection.ExecuteAsync(
            "INSERT INTO jira_connection (id, project_id, base_url, email, created_by) VALUES (@id, @project, @url, @email, @joel)",
            new { id, project = projectId, url = JiraWorld.Credentials.BaseUrl, email = JiraWorld.Credentials.Email, joel = SeedIds.Users.Joel });
        _connections.Arrange(id, JiraWorld.Credentials);
        return id;
    }

    protected override async Task<Guid> ArrangeEmptyProjectAsync() => await NewEmptyProjectAsync();

    protected override Task<Guid> ArrangeNonEmptyProjectAsync() => Task.FromResult(SeedIds.Projects.Ship);

    protected override Task<string> ArrangeTakenProjectKeyAsync() => Task.FromResult("SHIP");

    protected override async Task<Guid> ArrangeJobAsync(Guid connectionId, ImportJobState state, int pendingErrors)
    {
        var jobId = Guid.CreateVersion7();
        var finished = state is not (ImportJobState.Queued or ImportJobState.Running);
        var projectId = await NewEmptyProjectAsync();
        await using var connection = await _database.OpenAsync();
        await connection.ExecuteAsync(
            """
            INSERT INTO import_job (id, connection_id, jira_project_key, project_key, project_id, status, progress, started_at, finished_at, created_by)
            SELECT @jobId, @connectionId, @jiraKey, p.key, p.id, @status, @progress::jsonb, now(), CASE WHEN @finished THEN now() END, @joel FROM project p WHERE p.id = @projectId
            """,
            new
            {
                jobId, connectionId, jiraKey = JiraWorld.ProjectKey, status = ImportJobRows.StatusText(state), progress = ImportJson.Write(ProgressDocs.Initial(false)),
                finished, joel = SeedIds.Users.Joel, projectId,
            });
        await _host.Services.GetRequiredService<ImportJobRows>().SaveMappingAsync(jobId, _mapping, CancellationToken.None);
        for (var i = 0; i < pendingErrors; i++)
        {
            var key = $"PLAT-{Interlocked.Increment(ref _nextIssue) - 1}";
            await connection.ExecuteAsync(
                "INSERT INTO import_error (id, import_job_id, step, entity, source_key, message) VALUES (@id, @jobId, 'issues', 'issue', @key, 'Jira timed out (504)')",
                new { id = Guid.CreateVersion7(), jobId, key });
        }

        return jobId;
    }

    private async Task<Guid> NewEmptyProjectAsync()
    {
        var key = $"Z{(char)('A' + (Interlocked.Increment(ref _nextProject) / 26 % 26))}{(char)('A' + (_nextProject % 26))}";
        var project = await _scope.ServiceProvider.GetRequiredService<IProjectFactory>().CreateAsync(new ProjectCreate($"Arranged {key}", key, null, [], [1, 2, 3, 4, 5], null));
        return project.Id;
    }
}
