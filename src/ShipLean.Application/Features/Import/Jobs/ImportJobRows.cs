using System.Globalization;
using Agoda.IoC.Core;
using Dapper;
using Npgsql;
using ShipLean.Application.Identity;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Import;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using ShipLean.Core;

namespace ShipLean.Application.Features.Import.Jobs;

// Every read and write of the import tables: jobs, their mapping, log and errors. Statuses and resolutions are stored as camelCase text.
[RegisterSingleton]
public sealed class ImportJobRows(NpgsqlDataSource dataSource, TimeProvider clock)
{
    private const string IssueTypeKind = "issueType";
    private const string StatusKind = "status";
    private const string UserKind = "user";
    private const string FieldKind = "field";
    private const string Everything = "*";
    private const string FieldsSource = "fields";
    private const string TargetSource = "target";

    private const string SelectJob =
        $"""
        SELECT j.id, j.connection_id AS connectionId, j.jira_project_key AS jiraProjectKey, j.project_key AS projectKey, j.project_id AS projectId,
               j.status, j.progress::text AS progress, j.started_at AS startedAt, j.finished_at AS finishedAt,
               u.id AS creatorId, u.name AS creatorName, u.initials AS creatorInitials, u.avatar_colour AS creatorColour, {UserRefs.PhotoUrlSql} AS creatorPhotoUrl
        FROM import_job j
        LEFT JOIN app_user u ON u.id = j.created_by
        """;

    private const string FindSql = SelectJob + " WHERE j.id = @jobId";

    private const string ListSql =
        SelectJob +
        """

        WHERE (@projectId::uuid IS NULL OR j.project_id = @projectId)
          AND EXISTS (SELECT 1 FROM project_member m
                      WHERE m.user_id = @viewer AND m.role = 'admin' AND m.archived_at IS NULL
                        AND (m.project_id = j.project_id OR m.project_id = (SELECT c.project_id FROM jira_connection c WHERE c.id = j.connection_id)))
        ORDER BY j.created_at DESC, j.id DESC
        """;

    public static string StatusText(ImportJobState state) => System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(state.ToString());

    public static ImportJobState StatusOf(string text) => Enum.Parse<ImportJobState>(text, ignoreCase: true);

    public async Task<ImportJobRow?> FindAsync(Guid jobId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<ImportJobRow>(new CommandDefinition(FindSql, new { jobId }, cancellationToken: cancellationToken));
    }

    public async Task<ImportJobRow> GetAsync(Guid jobId, CancellationToken cancellationToken)
        => await FindAsync(jobId, cancellationToken) ?? throw ApiException.NotFound("That import does not exist.");

    // Newest first. Only jobs the viewer administers: the project the connection belongs to, or the project the import went into.
    public async Task<IReadOnlyList<ImportJobRow>> ListAsync(Guid? projectId, Guid viewer, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<ImportJobRow>(new CommandDefinition(
            ListSql,
            new { projectId, viewer }, cancellationToken: cancellationToken));
        return [.. rows];
    }

    private const string CurrentConnectionSql =
        """
        SELECT cur.id FROM import_job j
        JOIN jira_connection old ON old.id = j.connection_id
        JOIN jira_connection cur ON cur.project_id = old.project_id AND cur.archived_at IS NULL
        WHERE j.id = @jobId
        """;

    // Replacing a Jira connection archives the old one, so a job made with it carries on with the project's current connection: the job is pointed at it. Null when the
    // project has none now.
    public async Task<Guid?> CurrentConnectionAsync(Guid jobId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var current = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(CurrentConnectionSql, new { jobId }, cancellationToken: cancellationToken));
        if (current is { } id)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE import_job SET connection_id = @id, updated_at = now() WHERE id = @jobId AND connection_id <> @id", new { id, jobId }, cancellationToken: cancellationToken));
        }

        return current;
    }

    public async Task<bool> ConnectionExistsAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM jira_connection WHERE id = @connectionId AND archived_at IS NULL)", new { connectionId }, cancellationToken: cancellationToken));
    }

    public async Task<string?> ProjectKeyOfAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<string?>(new CommandDefinition("SELECT key FROM project WHERE id = @projectId", new { projectId }, cancellationToken: cancellationToken));
    }

    public async Task<bool> ProjectKeyTakenAsync(string key, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition("SELECT EXISTS (SELECT 1 FROM project WHERE key = @key)", new { key }, cancellationToken: cancellationToken));
    }

    public async Task<bool> ProjectHasWorkItemsAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition("SELECT EXISTS (SELECT 1 FROM work_item WHERE project_id = @projectId)", new { projectId }, cancellationToken: cancellationToken));
    }

    // A queued or running job for the same Jira project through the same connection, into the same existing project, or creating the same new project key.
    public async Task<bool> ActiveJobExistsAsync(Guid connectionId, string jiraProjectKey, Guid? projectId, string projectKey, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT EXISTS (SELECT 1 FROM import_job
                           WHERE status IN ('queued', 'running')
                             AND ((connection_id = @connectionId AND jira_project_key = @jiraProjectKey) OR (@projectId::uuid IS NOT NULL AND project_id = @projectId)
                                  OR (@projectId::uuid IS NULL AND project_id IS NULL AND project_key = @projectKey)))
            """,
            new { connectionId, jiraProjectKey, projectId, projectKey }, cancellationToken: cancellationToken));
    }

    public async Task InsertAsync(
        Guid id, ImportStartRequest request, string projectKey, Guid? projectId, Guid createdBy, string firstLogLine, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO import_job (id, connection_id, jira_project_key, project_key, project_id, status, progress, started_at, created_by, created_at, updated_at)
            VALUES (@id, @connectionId, @jiraProjectKey, @projectKey, @projectId, 'queued', @progress::jsonb, @now, @createdBy, @now, @now)
            """,
            new
            {
                id, connectionId = request.ConnectionId, jiraProjectKey = request.JiraProjectKey.Trim(), projectKey, projectId,
                progress = ImportJson.Write(ProgressDocs.Initial(false)), now, createdBy,
            },
            transaction, cancellationToken: cancellationToken));
        await SaveMappingAsync(connection, transaction, id, request.Mapping, request.Target, cancellationToken);
        await LogAsync(connection, transaction, id, ImportLogLevel.Info, firstLogLine, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SaveMappingAsync(Guid jobId, ImportMappingDto mapping, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SaveMappingAsync(connection, transaction, jobId, mapping, null, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    // The mapping is four rows (types, statuses, users, fields), each replaced whole, so a re-run's edit leaves nothing of the old choices behind. Totals and warnings are
    // the server's to compute and are not stored.
    private static async Task SaveMappingAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid jobId, ImportMappingDto mapping, ImportTarget? target, CancellationToken cancellationToken)
    {
        await UpsertAsync(connection, transaction, jobId, IssueTypeKind, Everything, ImportJson.Write(mapping.IssueTypes), cancellationToken);
        await UpsertAsync(connection, transaction, jobId, StatusKind, Everything, ImportJson.Write(mapping.Statuses), cancellationToken);
        await UpsertAsync(connection, transaction, jobId, UserKind, Everything, ImportJson.Write(mapping.Users), cancellationToken);
        await UpsertAsync(connection, transaction, jobId, FieldKind, FieldsSource, ImportJson.Write(mapping.Fields), cancellationToken);
        if (target is not null)
        {
            await UpsertAsync(connection, transaction, jobId, FieldKind, TargetSource, ImportJson.Write(target), cancellationToken);
        }
    }

    private static Task UpsertAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid jobId, string kind, string source, string json, CancellationToken cancellationToken)
        => connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO import_mapping (id, import_job_id, kind, source_value, target) VALUES (@id, @jobId, @kind, @source, @json::jsonb)
            ON CONFLICT (import_job_id, kind, source_value) DO UPDATE SET target = EXCLUDED.target, updated_at = now()
            """,
            new { id = Guid.CreateVersion7(), jobId, kind, source, json }, transaction, cancellationToken: cancellationToken));

    private sealed record MappingRow(string Kind, string SourceValue, string Target);

    public async Task<ImportMappingDto> ReadMappingAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var stored = await ReadMappingRowsAsync(jobId, cancellationToken);
        return new ImportMappingDto(
            Read<IReadOnlyList<IssueTypeMappingDto>>(stored, IssueTypeKind, Everything) ?? [],
            Read<IReadOnlyList<StatusMappingDto>>(stored, StatusKind, Everything) ?? [],
            Read<IReadOnlyList<UserMappingDto>>(stored, UserKind, Everything) ?? [],
            Read<ImportFieldsDto>(stored, FieldKind, FieldsSource) ?? new ImportFieldsDto(string.Empty, [], true, true, true, true),
            new ImportTotalsDto(0, 0, 0, 0, 0, 0), []);
    }

    // Where the job's work goes: a new project (key and name) or an existing empty one.
    public async Task<ImportTarget?> ReadTargetAsync(Guid jobId, CancellationToken cancellationToken)
        => Read<ImportTarget>(await ReadMappingRowsAsync(jobId, cancellationToken), FieldKind, TargetSource);

    private async Task<List<MappingRow>> ReadMappingRowsAsync(Guid jobId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<MappingRow>(new CommandDefinition(
            "SELECT kind, source_value AS sourceValue, target::text AS target FROM import_mapping WHERE import_job_id = @jobId", new { jobId }, cancellationToken: cancellationToken));
        return [.. rows];
    }

    private static T? Read<T>(List<MappingRow> rows, string kind, string source)
        => rows.FirstOrDefault(r => r.Kind == kind && r.SourceValue == source) is { } row ? ImportJson.Read<T>(row.Target) : default;

    private const string RunningSql = "UPDATE import_job SET status = 'running', progress = @progress::jsonb, updated_at = now() WHERE id = @jobId AND status = 'queued'";

    private const string StopQueuedSql =
        "UPDATE import_job SET status = 'stopped', progress = @progress::jsonb, finished_at = @now, updated_at = now() WHERE id = @jobId AND status = 'queued'";

    // The person who asks for the re-run becomes the job's actor: the run acts as created_by.
    private const string RequeueSql =
        """
        UPDATE import_job SET status = 'queued', progress = @progress::jsonb, started_at = @now, finished_at = NULL, created_by = @actor, updated_at = now()
        WHERE id = @jobId AND status NOT IN ('queued', 'running')
        """;

    private const string FinishSql =
        "UPDATE import_job SET status = @status, progress = @progress::jsonb, finished_at = @now, updated_at = now() WHERE id = @jobId";

    private const string ProgressSql = "UPDATE import_job SET progress = @progress::jsonb, updated_at = now() WHERE id = @jobId";

    // queued -> running. False when the job is no longer queued (it was stopped while it waited).
    public Task<bool> MarkRunningAsync(Guid jobId, ImportJobProgressJson progress, CancellationToken cancellationToken)
        => ExecuteAsync(RunningSql, jobId, progress, null, cancellationToken);

    // queued -> stopped at once.
    public Task<bool> StopQueuedAsync(Guid jobId, ImportJobProgressJson progress, CancellationToken cancellationToken)
        => ExecuteAsync(StopQueuedSql, jobId, progress, null, cancellationToken);

    // A finished job back to queued as a re-run. False when it is still queued or running.
    public async Task<bool> RequeueAsync(Guid jobId, Guid actor, ImportJobProgressJson progress, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(
            RequeueSql, new { jobId, actor, progress = ImportJson.Write(progress), now = clock.GetUtcNow() }, cancellationToken: cancellationToken)) == 1;
    }

    // Why the person a job runs as may not run it now, or null when they may: their account is switched off, or they no longer administer the project that owns the Jira
    // connection or the project the import goes into.
    public async Task<string?> ActorProblemAsync(Guid jobId, Guid actor, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var state = await connection.QuerySingleOrDefaultAsync<(bool Active, bool Admin)>(new CommandDefinition(
            """
            SELECT u.disabled_at IS NULL AS active,
                   EXISTS (SELECT 1 FROM project_member m
                           WHERE m.user_id = u.id AND m.role = 'admin' AND m.archived_at IS NULL
                             AND (m.project_id = j.project_id OR m.project_id = c.project_id)) AS admin
            FROM import_job j
            JOIN jira_connection c ON c.id = j.connection_id
            JOIN app_user u ON u.id = @actor
            WHERE j.id = @jobId
            """,
            new { jobId, actor }, cancellationToken: cancellationToken));
        if (!state.Active)
        {
            return "their account is switched off";
        }

        return state.Admin ? null : "they are no longer an admin of the project";
    }

    // The project this key names, when it is the half-made result of this job: empty, and administered by the job's actor alone.
    public async Task<Guid?> FindAdoptableProjectAsync(string key, Guid actor, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            """
            SELECT p.id FROM project p
            WHERE p.key = @key
              AND NOT EXISTS (SELECT 1 FROM work_item w WHERE w.project_id = p.id)
              AND (SELECT count(*) FROM project_member m WHERE m.project_id = p.id AND m.role = 'admin' AND m.archived_at IS NULL) = 1
              AND EXISTS (SELECT 1 FROM project_member m WHERE m.project_id = p.id AND m.role = 'admin' AND m.archived_at IS NULL AND m.user_id = @actor)
            """,
            new { key, actor }, cancellationToken: cancellationToken));
    }

    // The runner's last resort when a job's own failure handling threw: a job left Running can be neither stopped nor re-run.
    public async Task FailRunningAsync(Guid jobId, string message, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var failed = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE import_job SET status = 'failed', finished_at = @now, updated_at = now(), progress = jsonb_set(progress, '{message}', to_jsonb(@message::text))
            WHERE id = @jobId AND status = 'running'
            """,
            new { jobId, message, now = clock.GetUtcNow() }, cancellationToken: cancellationToken));
        if (failed == 1)
        {
            await LogAsync(connection, null, jobId, ImportLogLevel.Error, message, cancellationToken);
        }
    }

    public Task<bool> FinishAsync(Guid jobId, ImportJobState state, ImportJobProgressJson progress, CancellationToken cancellationToken)
        => ExecuteAsync(FinishSql, jobId, progress, StatusText(state), cancellationToken);

    public Task<bool> SaveProgressAsync(Guid jobId, ImportJobProgressJson progress, CancellationToken cancellationToken)
        => ExecuteAsync(ProgressSql, jobId, progress, null, cancellationToken);

    // A job the process died in the middle of goes back to the queue; returns every queued job, oldest first.
    public async Task<IReadOnlyList<Guid>> ResumeInterruptedAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var ids = await connection.QueryAsync<Guid>(new CommandDefinition(
            "UPDATE import_job SET status = 'queued', updated_at = now() WHERE status = 'running' RETURNING id", cancellationToken: cancellationToken));
        foreach (var id in ids)
        {
            await LogAsync(connection, null, id, ImportLogLevel.Info, "Picked up again after a restart", cancellationToken);
        }

        return [.. await connection.QueryAsync<Guid>(new CommandDefinition("SELECT id FROM import_job WHERE status = 'queued' ORDER BY started_at, id", cancellationToken: cancellationToken))];
    }

    private async Task<bool> ExecuteAsync(string sql, Guid jobId, ImportJobProgressJson progress, string? status, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var changed = await connection.ExecuteAsync(new CommandDefinition(
            sql, new { jobId, progress = ImportJson.Write(progress), now = clock.GetUtcNow(), status }, cancellationToken: cancellationToken));
        return changed == 1;
    }

    public async Task SetProjectAsync(Guid jobId, Guid projectId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("UPDATE import_job SET project_id = @projectId, updated_at = now() WHERE id = @jobId", new { jobId, projectId }, cancellationToken: cancellationToken));
    }

    // A Jira-imported project keeps "Sprint N": its imported sprints are numbered that way.
    public async Task UseNumberSprintNamingAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("UPDATE project SET sprint_naming = 'number' WHERE id = @projectId", new { projectId }, cancellationToken: cancellationToken));
    }

    public async Task LogAsync(Guid jobId, ImportLogLevel level, string message, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await LogAsync(connection, null, jobId, level, message, cancellationToken);
    }

    private Task LogAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, Guid jobId, ImportLogLevel level, string message, CancellationToken cancellationToken)
        => connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO import_log (import_job_id, logged_at, level, message) VALUES (@jobId, @at, @level, @message)",
            new { jobId, at = clock.GetUtcNow(), level = LevelText(level), message }, transaction, cancellationToken: cancellationToken));

    private static string LevelText(ImportLogLevel level) => level.ToString().ToLowerInvariant();

    private sealed record LogRow(long Id, DateTimeOffset LoggedAt, string Level, string Message);

    public async Task<IReadOnlyList<ImportLogLineDto>> LogLinesAsync(Guid jobId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var lines = await connection.QueryAsync<LogRow>(new CommandDefinition(
            "SELECT id, logged_at AS loggedAt, level, message FROM import_log WHERE import_job_id = @jobId ORDER BY id", new { jobId }, cancellationToken: cancellationToken));
        return [.. lines.Select(l => new ImportLogLineDto(
            l.Id, l.LoggedAt, TimeZoneInfo.ConvertTime(l.LoggedAt, clock.LocalTimeZone).ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            Enum.Parse<ImportLogLevel>(l.Level, ignoreCase: true), l.Message))];
    }

    private const string SelectError = "SELECT id, import_job_id AS jobId, step, entity, source_key AS sourceKey, message, resolution FROM import_error";

    public async Task<ImportErrorRow?> FindErrorAsync(Guid errorId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<ImportErrorRow>(new CommandDefinition($"{SelectError} WHERE id = @errorId", new { errorId }, cancellationToken: cancellationToken));
    }

    // Pending first, then resolved, each oldest first.
    public async Task<IReadOnlyList<ImportErrorRow>> ErrorsAsync(Guid jobId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return [.. await connection.QueryAsync<ImportErrorRow>(new CommandDefinition(
            $"{SelectError} WHERE import_job_id = @jobId ORDER BY (resolution <> 'pending'), created_at, id", new { jobId }, cancellationToken: cancellationToken))];
    }

    public async Task<IReadOnlyList<ImportErrorRow>> PendingErrorsAsync(Guid jobId, CancellationToken cancellationToken)
        => [.. (await ErrorsAsync(jobId, cancellationToken)).Where(e => e.Resolution == ImportErrorRow.Pending)];

    // pending -> retried or skipped. False when it is already resolved.
    public async Task<bool> ResolveErrorAsync(Guid errorId, ImportErrorResolution resolution, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE import_error SET resolution = @resolution, updated_at = now() WHERE id = @errorId AND resolution = 'pending'",
            new { errorId, resolution = ResolutionText(resolution) }, cancellationToken: cancellationToken)) == 1;
    }

    private static string ResolutionText(ImportErrorResolution resolution) => resolution.ToString().ToLowerInvariant();

    // After a retry or a run: of the pending rows that name the same thing, only the newest stays pending, so one failing item is one row.
    public async Task KeepNewestPendingAsync(Guid jobId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE import_error e SET resolution = 'retried', updated_at = now()
            WHERE e.import_job_id = @jobId AND e.resolution = 'pending'
              AND EXISTS (SELECT 1 FROM import_error n
                          WHERE n.import_job_id = e.import_job_id AND n.resolution = 'pending' AND n.entity = e.entity AND n.source_key = e.source_key
                            AND (n.created_at, n.id) > (e.created_at, e.id))
            """,
            new { jobId }, cancellationToken: cancellationToken));
    }

    // DoneWithErrors -> Done once no error is pending. True when it moved.
    public async Task<bool> FinishIfResolvedAsync(Guid jobId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var moved = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE import_job SET status = 'done', updated_at = now()
            WHERE id = @jobId AND status = 'doneWithErrors'
              AND NOT EXISTS (SELECT 1 FROM import_error WHERE import_job_id = @jobId AND resolution = 'pending')
            """,
            new { jobId }, cancellationToken: cancellationToken)) == 1;
        if (moved)
        {
            await LogAsync(connection, null, jobId, ImportLogLevel.Info, "Every error is retried or skipped", cancellationToken);
        }

        return moved;
    }

    // Pending errors by entity ("issue", "comment", "attachment").
    public async Task<IReadOnlyDictionary<string, int>> PendingCountsAsync(Guid jobId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var counts = await connection.QueryAsync<(string Entity, int Count)>(new CommandDefinition(
            "SELECT entity AS Entity, count(*)::int AS Count FROM import_error WHERE import_job_id = @jobId AND resolution = 'pending' GROUP BY entity",
            new { jobId }, cancellationToken: cancellationToken));
        return counts.ToDictionary(c => c.Entity, c => c.Count);
    }

    public ImportJobDto ToDto(ImportJobRow row)
        => new(
            row.Id, row.JiraProjectKey, row.ProjectKey, row.ProjectId, StatusOf(row.Status), ImportJson.Read<ImportJobProgressJson>(row.Progress),
            row.StartedAt, row.StartedAt is { } started ? DisplayDate.Relative(started, clock) : null,
            row.FinishedAt, row.FinishedAt is { } finished ? DisplayDate.Relative(finished, clock) : null,
            row.CreatorId is { } creator ? new UserRef(creator, row.CreatorName!, row.CreatorInitials!, row.CreatorColour!, row.CreatorPhotoUrl) : null);

    public static ImportErrorDto ToDto(ImportErrorRow row)
        => new(row.Id, Enum.Parse<ImportStep>(row.Step, ignoreCase: true), row.Entity, row.SourceKey, row.Message, Enum.Parse<ImportErrorResolution>(row.Resolution, ignoreCase: true));
}
