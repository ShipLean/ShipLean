using System.Text.Json;
using Dapper;
using Npgsql;
using ShipLean.Application.Features.Retros.Reads;
using ShipLean.Application.Features.Retros.Store;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Retros;

namespace ShipLean.Application.Features.Retros.Log;

// What the MCP tools log_retrospective and list_retrospectives do. A retro belongs to one item and one agent: the same agent
// logging on the same item again appends to it. Points are themed and inserted one at a time, so the theme weights are right.
public sealed class RetroLog(NpgsqlDataSource dataSource, IRetroThemer themer, IRetroCards cards, ICurrentUser user, ICurrentAgent agent, IProjectEvents events) : IRetroLog
{
    public const int MaxTextLength = 2000;
    public const int MaxListed = 200;

    private static readonly string ListSql = $"""
        SELECT p.id, p.retrospective_id AS retroId
        FROM retro_point p
        JOIN retrospective r ON r.id = p.retrospective_id
        LEFT JOIN work_item w ON w.id = r.work_item_id
        LEFT JOIN retro_theme t ON t.id = p.theme_id
        WHERE r.project_id = @projectId AND r.archived_at IS NULL AND p.archived_at IS NULL
          AND (@sprintId::uuid IS NULL OR r.sprint_id = @sprintId)
          AND (@featureId::uuid IS NULL OR r.work_item_id = @featureId OR (w.parent_id = @featureId AND w.type IN ('story', 'bug')))
          AND (NOT @noSprint OR r.sprint_id IS NULL)
          AND (NOT @undecided OR {RetroDecisionSql.Undecided("p")})
          AND (@category::text IS NULL OR p.category = @category)
          AND (@theme::text IS NULL OR lower(t.name) = lower(@theme))
          AND (@since::timestamptz IS NULL OR p.created_at >= @since)
        ORDER BY p.created_at DESC, p.position DESC, p.id
        LIMIT @limit
        """;

    private const string AutoThemeWarning = "Themes are assigned by ShipLean; theme ignored";

    public async Task<RetroLogResult> LogAsync(RetroLogRequest request, CancellationToken cancellationToken = default)
    {
        var points = Flatten(request);
        var failures = FailureLines(request.FailureLog);
        Validate(points, failures);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var item = await LoadItemAsync(connection, request, cancellationToken);
        var warnings = new List<string>();
        var sprintId = await RetroSprints.ResolveAsync(connection, item.Id, null, cancellationToken);
        var agentName = AgentStamp.Of(agent);
        var role = request.CompiledFrom.Count > 0 || item.Type == "feature" ? RetroRole.Conductor : RetroRole.SubAgent;
        var sources = await LoadSourcesAsync(connection, request, warnings, cancellationToken);

        Guid retroId;
        RetroRole storedRole;
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            // An append keeps the stored sprint and may upgrade the stored role, so the event and the reply use what the row holds.
            var stored = await UpsertRetroAsync(connection, transaction, request, item.Id, sprintId, agentName, role, cancellationToken);
            (retroId, sprintId, storedRole) = (stored.Id, stored.SprintId, ParseRole(stored.Role));
            var position = await NextPointPositionAsync(connection, transaction, retroId, cancellationToken);
            foreach (var point in points)
            {
                var supplied = string.IsNullOrWhiteSpace(point.Theme) ? null : point.Theme.Trim();
                if (supplied is not null && themer.Mode == RetroThemeMode.Auto)
                {
                    warnings.Add(AutoThemeWarning);
                    supplied = null;
                }

                var theme = await themer.AssignAsync(new RetroThemeRequest(request.ProjectId, point.Text, point.RootCause, supplied, false), cancellationToken);
                if (theme.Warning is { } warning)
                {
                    warnings.Add(warning);
                }

                await RetroPointRows.InsertAsync(connection, transaction, retroId, point.Category, point.Text, point.RootCause, theme, user.Id, agentName, position++, cancellationToken);
            }

            await InsertFailuresAsync(connection, transaction, retroId, failures, cancellationToken);
            foreach (var source in sources)
            {
                await connection.ExecuteAsync(
                    new CommandDefinition(
                        "INSERT INTO retrospective_source (retrospective_id, source_retrospective_id) VALUES (@retroId, @source) ON CONFLICT DO NOTHING",
                        new { retroId, source },
                        transaction,
                        cancellationToken: cancellationToken));
            }

            await transaction.CommitAsync(cancellationToken);
        }

        await events.PublishAsync(new RetroChanged(request.ProjectId, sprintId is { } sprint ? [sprint] : [], [item.Id]));
        var itemRef = new RetroItemRefDto(item.Id, item.Key, new TypeRef(Enum.Parse<WorkItemTypeId>(item.Type, ignoreCase: true), WorkItemTypeRules.Name(Enum.Parse<WorkItemTypeId>(item.Type, ignoreCase: true))), item.Title);
        return new RetroLogResult(retroId, itemRef, storedRole, points.Count, themer.Mode, await RetroThemeList.LoadAsync(connection, request.ProjectId, cancellationToken), [.. warnings.Distinct()]);
    }

    public async Task<RetroListResult> ListAsync(RetroListRequest request, CancellationToken cancellationToken = default)
    {
        if (request.NoSprint && request.SprintId is not null)
        {
            throw ApiException.Validation("no_sprint and sprint cannot be combined: a point with no sprint has no sprint to name.", "no_sprint");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        // One more than the cap, so the caller can tell the list was cut. The filters run in SQL, before the cap.
        var rows = (await connection.QueryAsync<ListedPoint>(
            new CommandDefinition(
                ListSql,
                new
                {
                    projectId = request.ProjectId,
                    sprintId = request.SprintId,
                    featureId = request.FeatureId,
                    category = request.Category is { } category ? JsonNamingPolicy.CamelCase.ConvertName(category.ToString()) : null,
                    theme = request.Theme,
                    since = request.Since is { } since ? new DateTime(since.Year, since.Month, since.Day, 0, 0, 0, DateTimeKind.Utc) : (DateTime?)null,
                    noSprint = request.NoSprint,
                    undecided = request.Undecided,
                    limit = MaxListed + 1,
                },
                cancellationToken: cancellationToken))).ToList();

        var loaded = await cards.LoadAsync([.. rows.Select(row => row.RetroId).Distinct()], cancellationToken);
        var byId = loaded.SelectMany(card => card.Points).ToDictionary(point => point.Id);
        var points = rows.Where(row => byId.ContainsKey(row.Id)).Select(row => byId[row.Id]).ToList();
        var truncated = points.Count > MaxListed;
        return new RetroListResult(themer.Mode, [.. points.Take(MaxListed)], await RetroThemeList.LoadAsync(connection, request.ProjectId, cancellationToken), truncated);
    }

    private static List<Flat> Flatten(RetroLogRequest request)
        => [
            .. request.WentWell.Select(p => new Flat(RetroCategory.WentWell, p.Text, p.Theme, p.RootCause)),
            .. request.WentBadly.Select(p => new Flat(RetroCategory.WentBadly, p.Text, p.Theme, string.IsNullOrWhiteSpace(p.RootCause) ? null : p.RootCause)),
            .. request.Improve.Select(p => new Flat(RetroCategory.Improve, p.Text, p.Theme, p.RootCause)),
            .. request.NotInPlan.Select(p => new Flat(RetroCategory.NotInPlan, p.Text, p.Theme, p.RootCause)),
        ];

    private static List<(string Kind, string Text)> FailureLines(RetroFailureLogDto log)
        => [
            .. log.Blockers.Select(text => ("blocker", text)),
            .. log.AssumptionsDisproved.Select(text => ("assumptionDisproved", text)),
            .. log.Friction.Select(text => ("friction", text)),
        ];

    private static void Validate(List<Flat> points, List<(string Kind, string Text)> failures)
    {
        if (points.Count == 0 && failures.Count == 0)
        {
            throw ApiException.Validation("Log at least one point or a failure log.", "went_well");
        }

        foreach (var point in points)
        {
            if (string.IsNullOrWhiteSpace(point.Text))
            {
                throw ApiException.Validation("Every point needs text.", "text");
            }
            if (point.Text.Length > MaxTextLength)
            {
                throw ApiException.Validation($"A point is at most {MaxTextLength} characters.", "text");
            }
            if (point.RootCause is not null && point.Category != RetroCategory.WentBadly)
            {
                throw ApiException.Validation("root_cause is only for went_badly points.", "root_cause");
            }
            if (point.RootCause is { Length: > MaxTextLength })
            {
                throw ApiException.Validation($"A root_cause is at most {MaxTextLength} characters.", "root_cause");
            }
        }

        foreach (var (_, text) in failures)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw ApiException.Validation("A failure log entry cannot be blank.", "failure_log");
            }
            if (text.Length > MaxTextLength)
            {
                throw ApiException.Validation($"A failure log entry is at most {MaxTextLength} characters.", "failure_log");
            }
        }
    }

    private static async Task<ItemRow> LoadItemAsync(NpgsqlConnection connection, RetroLogRequest request, CancellationToken cancellationToken)
    {
        var item = await connection.QuerySingleOrDefaultAsync<ItemRow>(
            new CommandDefinition(
                "SELECT id, key, type, title FROM work_item WHERE id = @id AND project_id = @projectId",
                new { id = request.WorkItemId, projectId = request.ProjectId },
                cancellationToken: cancellationToken))
            ?? throw ApiException.NotFound("No work item with that key was found.");
        if (item.Type != "feature" && !WorkItemTypeRules.StoryLevelDb.Contains(item.Type))
        {
            throw ApiException.Validation($"{item.Key} is not a story, bug, action item or Feature, so it has no retrospective. Log it on the story or Feature.", "item");
        }

        return item;
    }

    // The retros of the items the conductor compiled from. An item without a retro is skipped, with a warning.
    private static async Task<IReadOnlyList<Guid>> LoadSourcesAsync(NpgsqlConnection connection, RetroLogRequest request, List<string> warnings, CancellationToken cancellationToken)
    {
        if (request.CompiledFrom.Count == 0)
        {
            return [];
        }

        var ids = request.CompiledFrom.ToArray();
        var inProject = await connection.QueryAsync<string>(
            new CommandDefinition("SELECT key FROM work_item WHERE id = ANY(@ids) AND project_id = @projectId", new { ids, projectId = request.ProjectId }, cancellationToken: cancellationToken));
        if (inProject.Count() != ids.Distinct().Count())
        {
            throw ApiException.Validation("A compiled_from item is not in this project.", "compiled_from");
        }

        var retros = (await connection.QueryAsync<SourceRow>(
            new CommandDefinition(
                """
                SELECT r.id, w.key AS itemKey FROM retrospective r JOIN work_item w ON w.id = r.work_item_id
                WHERE r.work_item_id = ANY(@ids) AND r.archived_at IS NULL AND r.work_item_id <> @itemId
                """,
                new { ids, itemId = request.WorkItemId },
                cancellationToken: cancellationToken))).ToList();
        var withRetro = retros.Select(row => row.ItemKey).ToHashSet();
        foreach (var key in inProject.Where(key => !withRetro.Contains(key)).Order())
        {
            warnings.Add($"{key} has no retrospective yet, so it is not recorded as a source.");
        }

        return [.. retros.Select(row => row.Id)];
    }

    private async Task<StoredRetro> UpsertRetroAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, RetroLogRequest request, Guid itemId, Guid? sprintId, string? agentName, RetroRole role, CancellationToken cancellationToken)
        => await connection.QuerySingleAsync<StoredRetro>(
            new CommandDefinition(
                """
                INSERT INTO retrospective (id, project_id, work_item_id, sprint_id, author_user_id, agent_name, role, partial)
                VALUES (@id, @projectId, @itemId, @sprintId, @userId, @agentName, @role, @partial)
                ON CONFLICT (work_item_id, agent_name) WHERE agent_name IS NOT NULL AND archived_at IS NULL
                DO UPDATE SET updated_at = now(), partial = EXCLUDED.partial,
                    role = CASE WHEN EXCLUDED.role = 'conductor' THEN 'conductor' ELSE retrospective.role END
                RETURNING id, sprint_id AS sprintId, role
                """,
                new
                {
                    id = Guid.NewGuid(),
                    projectId = request.ProjectId,
                    itemId,
                    sprintId,
                    userId = user.Id,
                    agentName,
                    role = JsonNamingPolicy.CamelCase.ConvertName(role.ToString()),
                    partial = request.Partial,
                },
                transaction,
                cancellationToken: cancellationToken));

    private static async Task<int> NextPointPositionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid retroId, CancellationToken cancellationToken)
        => await connection.QuerySingleAsync<int>(
            new CommandDefinition("SELECT COALESCE(max(position) + 1, 0) FROM retro_point WHERE retrospective_id = @retroId", new { retroId }, transaction, cancellationToken: cancellationToken));

    private static async Task<int> NextFailurePositionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid retroId, CancellationToken cancellationToken)
        => await connection.QuerySingleAsync<int>(
            new CommandDefinition("SELECT COALESCE(max(position) + 1, 0) FROM retro_failure_log WHERE retrospective_id = @retroId", new { retroId }, transaction, cancellationToken: cancellationToken));

    private static async Task InsertFailuresAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid retroId, List<(string Kind, string Text)> failures, CancellationToken cancellationToken)
    {
        var position = await NextFailurePositionAsync(connection, transaction, retroId, cancellationToken);
        foreach (var (kind, text) in failures)
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    "INSERT INTO retro_failure_log (id, retrospective_id, kind, text, position) VALUES (@id, @retroId, @kind, @text, @position)",
                    new { id = Guid.NewGuid(), retroId, kind, text, position = position++ },
                    transaction,
                    cancellationToken: cancellationToken));
        }
    }

    private static RetroRole ParseRole(string role) => Enum.Parse<RetroRole>(role, ignoreCase: true);

    private sealed record StoredRetro(Guid Id, Guid? SprintId, string Role);

    private sealed record Flat(RetroCategory Category, string Text, string? Theme, string? RootCause);

    private sealed record ItemRow(Guid Id, string Key, string Type, string Title);

    private sealed record SourceRow(Guid Id, string ItemKey);

    private sealed record ListedPoint(Guid Id, Guid RetroId);
}
