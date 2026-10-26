using System.Text.Json;
using Dapper;
using Npgsql;
using ShipLean.Application.Database;
using ShipLean.Application.Features.WorkItems;
using ShipLean.Application.Markdown;
using ShipLean.Contracts;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport.Contracts.WorkItems;
using ShipLean.TestSupport.Database;

namespace ShipLean.WebApi.Tests.WorkItems.Store;

// What WorkItemStoreContract needs on Postgres: the store over a clone of the seeded database (its users are the ones
// the fixture names) and a new project for every test, with one state per category for each type, a Stories board with
// three columns and an import job. Column 1 is split (it has a Done half); the others are not. Column 0 maps the Proposed states of Story and Bug, column 1 the In Progress ones and
// column 2 the Completed ones; the Removed states map to no column.
public sealed class PostgresWorkItemStoreArrangement : IAsyncDisposable
{
    private static int _projects;
    private readonly NpgsqlDataSource _dataSource;

    private PostgresWorkItemStoreArrangement(ClonedDatabase database)
    {
        Database = database;
        _dataSource = NpgsqlDataSource.Create(DatabaseServices.PinUtc(database.ConnectionString));
    }

    public ClonedDatabase Database { get; }

    public RecordingProjectEvents Events { get; } = new();

    public static async Task<PostgresWorkItemStoreArrangement> CreateAsync() => new(await ShipLeanDatabase.CloneSeedAsync());

    public WorkItemStore CreateStore(IWorkItemRules rules, Guid actorId)
        => new(_dataSource, new FixedCurrentUser(actorId), rules, Events, new MarkdownRenderer(), TimeProvider.System, AgentIdentity.None);

    public WorkItemReads CreateReads() => new(_dataSource, new MarkdownRenderer(), TimeProvider.System);

    public async Task<ArrangedProject> ArrangeProjectAsync()
    {
        var projectId = Guid.NewGuid();
        var key = LetterKey(Interlocked.Increment(ref _projects));
        var states = new List<StateSnapshot>();
        foreach (var type in Enum.GetValues<WorkItemTypeId>())
        {
            states.Add(new(Guid.NewGuid(), type, "New", StateCategory.Proposed));
            states.Add(new(Guid.NewGuid(), type, "Active", StateCategory.InProgress));
            states.Add(new(Guid.NewGuid(), type, "Closed", StateCategory.Completed));
            states.Add(new(Guid.NewGuid(), type, "Removed", StateCategory.Removed));
        }

        string[] columns = [$"col-{Guid.NewGuid():N}", $"col-{Guid.NewGuid():N}", $"col-{Guid.NewGuid():N}"];
        var project = new ArrangedProject(projectId, key, states, columns, Guid.NewGuid());
        await using var connection = await Database.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await ExecuteAsync(connection, transaction, "INSERT INTO project (id, key, name) VALUES (@id, @key, @name)", new { id = projectId, key, name = $"Project {key}" });
        for (var i = 0; i < states.Count; i++)
        {
            var state = states[i];
            await ExecuteAsync(
                connection,
                transaction,
                "INSERT INTO state (id, project_id, type, name, category, sort_order) VALUES (@id, @projectId, @type, @name, @category, @order)",
                new { id = state.Id, projectId, type = state.Type.ToString().ToLowerInvariant(), name = state.Name, category = JsonNamingPolicy.CamelCase.ConvertName(state.Category.ToString()), order = i });
        }

        await ExecuteAsync(
            connection,
            transaction,
            "INSERT INTO board (id, project_id, level, settings) VALUES (@id, @projectId, 'stories', @settings::jsonb)",
            new { id = Guid.NewGuid(), projectId, settings = JsonSerializer.Serialize(StoriesBoard(project), PersistedJson.Options) });
        await InsertImportJobAsync(connection, transaction, project);
        await transaction.CommitAsync();
        return project;
    }

    public async ValueTask DisposeAsync()
    {
        await _dataSource.DisposeAsync();
        await Database.DisposeAsync();
    }

    private static BoardSettingsJson StoriesBoard(ArrangedProject project)
    {
        ColumnStateMapping[] Mappings(StateCategory category) =>
            [new(WorkItemTypeId.Story, project.StateId(WorkItemTypeId.Story, category)), new(WorkItemTypeId.Bug, project.StateId(WorkItemTypeId.Bug, category))];
        BoardColumnJson[] columns =
        [
            new(project.ColumnId(0), "New", null, false, "", Mappings(StateCategory.Proposed)),
            new(project.ColumnId(1), "In progress", null, true, "", Mappings(StateCategory.InProgress)),
            new(project.ColumnId(2), "Done", null, false, "", Mappings(StateCategory.Completed)),
        ];
        return new BoardSettingsJson(
            columns,
            [new SwimlaneJson("lane-default", "Everything else", null, SwimlaneKind.Default, null)],
            [],
            [],
            [],
            new CardAnnotationsJson(false, false, false, false),
            null);
    }

    private static async Task InsertImportJobAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, ArrangedProject project)
    {
        var connectionId = Guid.NewGuid();
        await ExecuteAsync(
            connection,
            transaction,
            "INSERT INTO jira_connection (id, project_id, base_url, email) VALUES (@id, @projectId, 'https://jira.example.test', 'import@example.test')",
            new { id = connectionId, projectId = project.ProjectId });
        var counts = new ImportEntityCount(0, 0, 0);
        var progress = new ImportJobProgressJson(100, null, "", false, new ImportCounts(counts, counts, counts, counts, counts), 0, 0, 0, null);
        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO import_job (id, connection_id, jira_project_key, project_key, project_id, status, progress)
            VALUES (@id, @connectionId, 'ARR', @key, @projectId, 'done', @progress::jsonb)
            """,
            new { id = project.ImportJobId, connectionId, key = project.Key, projectId = project.ProjectId, progress = JsonSerializer.Serialize(progress, PersistedJson.Options) });
    }

    private static Task ExecuteAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, object parameters)
        => connection.ExecuteAsync(sql, parameters, transaction);

    // Project keys are 2-4 letters (the schema's rule) and none of the seed's: T then the number in base 26 (TA, TB, ... TZ, TBA, ...).
    private static string LetterKey(int number)
    {
        var letters = string.Empty;
        do
        {
            letters = (char)('A' + (number % 26)) + letters;
            number /= 26;
        }
        while (number > 0);
        return "T" + letters;
    }
}
