using System.Data.Common;
using System.Text.Json;
using Dapper;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Boards;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Projects;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Sprints;

namespace ShipLean.Application.Features.Projects;

// IProjectFactory over Postgres. One transaction carries the project, its members, states, boards and optional Sprint 1, so a
// failure anywhere leaves no project behind. It enforces the instance's project_creation rule (read in the transaction, per
// request) and that the copy-from project exists; callers check the creator's membership of the copy-from project first
// (ProjectCreationGate).
public sealed class ProjectFactory(NpgsqlDataSource dataSource, TimeProvider clock, ICurrentUser currentUser, IBoardSettingsStore boards, ISprintStore sprints, IStateCatalog stateCatalog) : IProjectFactory
{
    private static readonly BoardLevel[] Levels = [BoardLevel.Stories, BoardLevel.Features, BoardLevel.Epics, BoardLevel.Taskboard];

    public async Task<ProjectDto> CreateAsync(ProjectCreate create, CancellationToken cancellationToken = default)
    {
        if (create is null || string.IsNullOrWhiteSpace(create.Name) || !ProjectKeys.IsValid(create.Key))
        {
            throw ApiException.Validation("A project needs a name and a key of two to four capital letters.");
        }
        if (create.WorkingDays is null || create.WorkingDays.Count == 0 || create.WorkingDays.Any(d => d is < 1 or > 7))
        {
            throw ApiException.Validation("Pick at least one working day.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await RequireMayCreateAsync(connection, transaction, cancellationToken);
        if (create.CopyFromProjectId is { } copyFrom && !await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                "SELECT EXISTS (SELECT 1 FROM project WHERE id = @copyFrom)", new { copyFrom }, transaction, cancellationToken: cancellationToken)))
        {
            throw ApiException.NotFound("There is no such project to copy.");
        }
        var memberIds = await MemberIdsAsync(connection, transaction, create.MemberEmails ?? [], cancellationToken);
        var projectId = Guid.CreateVersion7();
        var workingDays = create.WorkingDays.Distinct().Order().Select(d => (short)d).ToArray();
        var window = await InsertProjectAsync(connection, transaction, projectId, create, workingDays, cancellationToken);
        var memberCount = await InsertMembersAsync(connection, transaction, projectId, memberIds, cancellationToken);
        IReadOnlyDictionary<Guid, Guid>? copiedIds = null;
        if (create.CopyFromProjectId is { } source)
        {
            copiedIds = await stateCatalog.CopyAsync(source, projectId, transaction, cancellationToken);
        }
        else
        {
            await stateCatalog.CreateDefaultsAsync(projectId, transaction, cancellationToken);
        }
        var states = await ReadStatesAsync(connection, transaction, projectId, cancellationToken);
        await InsertBoardsAsync(connection, transaction, create.CopyFromProjectId, projectId, states, copiedIds, cancellationToken);
        if (create.FirstSprint is { } first)
        {
            await sprints.CreateAsync(projectId, first, transaction, cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return new ProjectDto(projectId, create.Key, create.Name.Trim(), string.Empty, ProjectRole.Admin, memberCount, [.. workingDays.Select(d => (int)d)], window, null, 1);
    }

    public async Task<KeyCheckDto> CheckKeyAsync(string key, string name, CancellationToken cancellationToken = default)
    {
        key ??= string.Empty;
        if (!ProjectKeys.IsValid(key))
        {
            return new KeyCheckDto(key, false, KeyCheckReason.Invalid, []);
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var mine = await connection.QuerySingleOrDefaultAsync<bool?>(new CommandDefinition(
            """
            SELECT EXISTS (SELECT 1 FROM project_member m WHERE m.project_id = p.id AND m.user_id = @me AND m.archived_at IS NULL)
            FROM project p WHERE p.key = @key
            """,
            new { key, me = currentUser.Id }, cancellationToken: cancellationToken));
        if (mine is null)
        {
            return new KeyCheckDto(key, true, null, []);
        }

        var candidates = ProjectKeys.Candidates(key, name).Where(c => c != key && ProjectKeys.IsValid(c)).Distinct().ToArray();
        var taken = (await connection.QueryAsync<string>(new CommandDefinition(
            "SELECT key FROM project WHERE key = ANY(@candidates)", new { candidates }, cancellationToken: cancellationToken))).ToHashSet();
        return new KeyCheckDto(key, false, mine.Value ? KeyCheckReason.Taken : KeyCheckReason.TakenByAnotherProject, [.. candidates.Where(c => !taken.Contains(c)).Take(3)]);
    }

    // Unknown (or disabled) emails are NotFound; the creator is added as Admin separately, so their own email is skipped.
    private async Task<IReadOnlyList<Guid>> MemberIdsAsync(DbConnection connection, DbTransaction transaction, IReadOnlyList<string> emails, CancellationToken cancellationToken)
    {
        var ids = new List<Guid>();
        foreach (var email in emails.Select(e => e?.Trim() ?? string.Empty).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var id = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
                "SELECT id FROM app_user WHERE lower(email) = lower(@email) AND disabled_at IS NULL", new { email }, transaction, cancellationToken: cancellationToken));
            ids.Add(id ?? throw ApiException.NotFound($"No user has the email {email}."));
        }
        return [.. ids.Where(id => id != currentUser.Id).Distinct()];
    }

    // The copied project's velocity window is kept; working days are the request's.
    private async Task<int> InsertProjectAsync(
        DbConnection connection, DbTransaction transaction, Guid projectId, ProjectCreate create, short[] workingDays, CancellationToken cancellationToken)
    {
        var inserted = await connection.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            """
            INSERT INTO project (id, key, name, working_days, velocity_window, created_by, created_at, updated_at)
            VALUES (@projectId, @key, @name, @workingDays, COALESCE((SELECT velocity_window FROM project WHERE id = @copyFrom), 3), @me, @now, @now)
            ON CONFLICT (key) DO NOTHING
            RETURNING velocity_window
            """,
            new { projectId, key = create.Key, name = create.Name.Trim(), workingDays, copyFrom = create.CopyFromProjectId, me = currentUser.Id, now = clock.GetUtcNow() },
            transaction, cancellationToken: cancellationToken));
        return inserted ?? throw ApiException.Conflict("That key is already taken.");
    }

    private async Task<int> InsertMembersAsync(DbConnection connection, DbTransaction transaction, Guid projectId, IReadOnlyList<Guid> memberIds, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var rows = new[] { (UserId: currentUser.Id, Role: "admin") }.Concat(memberIds.Select(id => (UserId: id, Role: "member"))).ToList();
        foreach (var row in rows)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT INTO project_member (id, project_id, user_id, role, created_at, updated_at) VALUES (@id, @projectId, @userId, @role, @now, @now)",
                new { id = Guid.CreateVersion7(), projectId, userId = row.UserId, row.Role, now }, transaction, cancellationToken: cancellationToken));
        }
        return rows.Count;
    }

    // The instance's project_creation rule; no settings row means the column's default, everyone.
    private async Task RequireMayCreateAsync(DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken)
    {
        var rule = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition("SELECT project_creation FROM instance_settings", transaction: transaction, cancellationToken: cancellationToken));
        if (rule != "admins")
        {
            return;
        }
        var isAdmin = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM app_user WHERE id = @me AND is_admin AND disabled_at IS NULL)", new { me = currentUser.Id }, transaction, cancellationToken: cancellationToken));
        if (!isAdmin)
        {
            throw ApiException.Forbidden("Only an instance admin can do that.");
        }
    }

    // The new project's live states, as the catalog just wrote them, in order.
    private static async Task<Dictionary<WorkItemTypeId, IReadOnlyList<StateRef>>> ReadStatesAsync(
        DbConnection connection, DbTransaction transaction, Guid projectId, CancellationToken cancellationToken)
    {
        var rows = await connection.QueryAsync<StateRow>(new CommandDefinition(
            "SELECT id, type, name, category, sort_order AS sortOrder FROM state WHERE project_id = @projectId AND archived_at IS NULL ORDER BY sort_order, name",
            new { projectId }, transaction, cancellationToken: cancellationToken));
        return rows.GroupBy(r => Enum.Parse<WorkItemTypeId>(r.Type, true)).ToDictionary(
            g => g.Key, g => (IReadOnlyList<StateRef>)[.. g.Select(r => new StateRef(r.Id, r.Name, Enum.Parse<StateCategory>(r.Category, true)))]);
    }

    // A copy takes the source's boards with every state id remapped; a level the source lacks gets the defaults over the new states.
    private async Task InsertBoardsAsync(
        DbConnection connection, DbTransaction transaction, Guid? source, Guid projectId, IReadOnlyDictionary<WorkItemTypeId, IReadOnlyList<StateRef>> states,
        IReadOnlyDictionary<Guid, Guid>? stateIds, CancellationToken cancellationToken)
    {
        var copied = new Dictionary<string, BoardSettingsJson>();
        if (source is { } from && stateIds is not null)
        {
            var rows = await connection.QueryAsync<(string Level, string Settings)>(new CommandDefinition(
                "SELECT level, settings::text FROM board WHERE project_id = @from", new { from }, transaction, cancellationToken: cancellationToken));
            foreach (var (level, settings) in rows)
            {
                var document = JsonSerializer.Deserialize<BoardSettingsJson>(settings, PersistedJson.Options)!;
                copied[level] = StateIdRemap.Apply(document, stateIds);
            }
        }
        foreach (var level in Levels)
        {
            var settings = copied.TryGetValue(level.ToString().ToLowerInvariant(), out var found) ? found : boards.BuildDefaults(level, states);
            await boards.InsertAsync(projectId, level, settings, transaction, cancellationToken);
        }
    }
}
