using System.Data.Common;
using Dapper;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Projects;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.ProjectSettings;

public sealed partial class ProjectSettingsService
{
    private const decimal LargestOverride = 999999.99m;

    // Saves every section or none. attemptedKey is the key the body carried, if any: it must equal the current key, because
    // a key is chosen at creation and never changes. Throws ApiException.Validation (blank name, no working day, a key
    // change, a bad forecast, a member listed twice, no admin left, or what IStateCatalog.ApplyAsync refuses),
    // ApiException.NotFound (a member email nobody has exactly, an unknown state) and ApiException.Conflict (an item
    // changed while its state was being removed).
    public async Task<SettingsSaveResult> SaveAsync(Guid projectId, ProjectSettingsPut put, string? attemptedKey, CancellationToken cancellationToken = default)
    {
        var current = await ReadProjectAsync(projectId, cancellationToken);
        if (put?.General is null || put.Members is null || put.Sprints is null || put.States is null || put.Members.Any(m => m is null))
        {
            throw ApiException.Validation("The settings need General, Members, Sprints and States.");
        }

        if (attemptedKey is not null && attemptedKey != current.Key)
        {
            throw ApiException.Validation("A project's key never changes.");
        }

        var general = await CheckAgentAsync(KeepUnprovided(CheckGeneral(put.General), current), current.AgentConnectionId, cancellationToken);
        CheckForecast(put.Sprints);
        var members = await ResolveMembersAsync(put.Members, cancellationToken);
        var moves = await PlanMovesAsync(projectId, put.States, cancellationToken);

        return await SaveAllAsync(projectId, put, general, members, moves, cancellationToken);
    }

    // One transaction, the work item store's. Items can only move into a state that exists, and the store runs its patches
    // before the InTransaction hook: so the states added in this save are inserted first, in BeforePatch, under temporary
    // names (their ids), and the hook gives them their real names with the other state changes. A failure anywhere rolls
    // everything back, so the same body can be sent again.
    private async Task<SettingsSaveResult> SaveAllAsync(
        Guid projectId, ProjectSettingsPut put, ProjectGeneralPut general, List<(Guid UserId, ProjectRole Role)> members, StateMoves moves, CancellationToken cancellationToken)
    {
        var added = put.States.Add ?? [];
        var stateChanges = put.States with
        {
            Add = [],
            Update = [.. put.States.Update ?? [], .. added.Select(a => new StateUpdate(a.Id, a.Name, a.Category, a.StartsAgent))],
        };
        IReadOnlyList<Guid> removedUsers = [];
        IReadOnlyList<(Guid BoardId, IReadOnlyList<string> Sections)> touchedBoards = [];
        var options = new WriteOptions(
            async (transaction, token) =>
            {
                await WriteProjectAsync(transaction, projectId, general, put.Sprints, token);
                removedUsers = await WriteMembersAsync(transaction, projectId, members, token);
                await RefuseLateItemsAsync(transaction, stateChanges, moves, token);
                await states.ApplyAsync(projectId, stateChanges, transaction, token);
                touchedBoards = await BoardMappingRepair.RunAsync(transaction, projectId, moves.Targets, token);
            },
            RevisionOrigin.User,
            added.Count == 0
                ? null
                : (transaction, token) => states.ApplyAsync(
                    projectId,
                    new StateChanges([.. added.Select(a => a with { Name = a.Id.ToString() })], [], [], []),
                    transaction,
                    token))
        { Bulk = true };
        var outcome = await workItems.PatchManyAsync(moves.Patches, options, cancellationToken);
        if (outcome.Conflicts is { Count: > 0 } stale)
        {
            throw ApiException.Conflict($"These items changed since the settings were loaded: {string.Join(", ", stale.Select(c => c.Current.Key))}. Nothing was saved.");
        }

        await events.PublishAsync(new ProjectChanged(projectId));
        if (HasStateChanges(put.States))
        {
            await events.PublishAsync(new StatesChanged(projectId));
        }

        foreach (var (boardId, sections) in touchedBoards)
        {
            await events.PublishAsync(new BoardSettingsChanged(projectId, boardId, sections));
        }

        return new SettingsSaveResult(await GetAsync(projectId, cancellationToken), removedUsers);
    }

    private static bool HasStateChanges(StateChanges changes)
        => changes.Add?.Count > 0 || changes.Update?.Count > 0 || changes.Reorder?.Count > 0 || changes.Remove?.Count > 0;

    private static ProjectGeneralPut CheckGeneral(ProjectGeneralPut general)
    {
        var days = general.WorkingDays?.Distinct().Order().ToArray() ?? [];
        if (string.IsNullOrWhiteSpace(general.Name))
        {
            throw ApiException.Validation("A project needs a name.");
        }

        if (days.Length == 0 || days.Any(d => d is < 1 or > 7))
        {
            throw ApiException.Validation("A project needs at least one working day, Monday (1) to Sunday (7).");
        }

        return new ProjectGeneralPut(
            general.Name.Trim(), general.Description ?? string.Empty, days, general.AgentConnectionId, ProjectAgentRules.RepoUrl(general.RepoUrl), ProjectAgentRules.BaseBranch(general.RepoBaseBranch), general.Provided);
    }

    // An agent field the body left out keeps its saved value (already valid, so it is not checked again).
    private static ProjectGeneralPut KeepUnprovided(ProjectGeneralPut general, ProjectRow current)
        => general with
        {
            AgentConnectionId = general.Provided.HasFlag(ProvidedAgentFields.Connection) ? general.AgentConnectionId : current.AgentConnectionId,
            RepoUrl = general.Provided.HasFlag(ProvidedAgentFields.RepoUrl) ? general.RepoUrl : current.RepoUrl,
            RepoBaseBranch = general.Provided.HasFlag(ProvidedAgentFields.RepoBaseBranch) ? general.RepoBaseBranch : current.RepoBaseBranch,
            Provided = ProvidedAgentFields.All,
        };

    // A connection can be picked only while it is not archived; keeping the one already chosen is always allowed.
    private async Task<ProjectGeneralPut> CheckAgentAsync(ProjectGeneralPut general, Guid? saved, CancellationToken cancellationToken)
    {
        if (general.AgentConnectionId is not { } id || id == saved)
        {
            return general;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var live = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM agent_connection WHERE id = @id AND archived_at IS NULL)", new { id }, cancellationToken: cancellationToken));
        return live ? general : throw ApiException.Validation("Pick an agent connection that is not archived.", "agentConnectionId");
    }

    private static void CheckForecast(ProjectForecastSettings forecast)
    {
        if (forecast.VelocityWindow < 1)
        {
            throw ApiException.Validation("The velocity window is at least one sprint.");
        }

        if (forecast.VelocityOverride is { } points && (points is < 0 or > LargestOverride || decimal.Round(points, 2) != points))
        {
            throw ApiException.Validation("The velocity override is zero or more points, with at most two decimal places.");
        }
    }

    // Exact full emails only: a partial email finds nobody.
    private async Task<List<(Guid UserId, ProjectRole Role)>> ResolveMembersAsync(IReadOnlyList<MemberPut> put, CancellationToken cancellationToken)
    {
        var members = new List<(Guid UserId, ProjectRole Role)>();
        foreach (var member in put)
        {
            if (string.IsNullOrWhiteSpace(member.Email) || !Enum.IsDefined(member.Role))
            {
                throw ApiException.Validation("Every member needs an email and a role.");
            }

            var user = await users.FindByEmailAsync(member.Email, cancellationToken)
                ?? throw ApiException.NotFound($"There is no user with the email {member.Email.Trim()}.");
            members.Add((user.Id, member.Role));
        }

        if (members.Select(m => m.UserId).Distinct().Count() != members.Count)
        {
            throw ApiException.Validation("A member can only be listed once.");
        }

        if (members.All(m => m.Role != ProjectRole.Admin))
        {
            throw ApiException.Validation("A project needs at least one admin.");
        }

        return members;
    }

    // The items in each removed state move to its target through the work item store, so each gets a revision.
    private async Task<StateMoves> PlanMovesAsync(Guid projectId, StateChanges changes, CancellationToken cancellationToken)
    {
        var removes = changes.Remove ?? [];
        if (removes.Count == 0)
        {
            return new StateMoves([], new Dictionary<Guid, Guid>());
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var live = (await connection.QueryAsync<(Guid Id, string Type)>(new CommandDefinition(
            "SELECT id, type FROM state WHERE project_id = @projectId AND archived_at IS NULL", new { projectId }, cancellationToken: cancellationToken))).ToDictionary(s => s.Id, s => Enum.Parse<WorkItemTypeId>(s.Type, ignoreCase: true));
        var fresh = (changes.Add ?? []).ToDictionary(a => a.Id, a => a.Type);
        foreach (var remove in removes)
        {
            if (!live.TryGetValue(remove.StateId, out var type) || !(live.TryGetValue(remove.TargetStateId, out var targetType) || fresh.TryGetValue(remove.TargetStateId, out targetType)))
            {
                throw ApiException.NotFound("There is no such state.");
            }

            if (type != targetType || remove.StateId == remove.TargetStateId || removes.Any(r => r.StateId == remove.TargetStateId))
            {
                throw ApiException.Validation("Items must move to another state of the same type.");
            }
        }

        var targets = removes.ToDictionary(r => r.StateId, r => r.TargetStateId);
        var items = await connection.QueryAsync<(Guid Id, int Revision, Guid StateId)>(new CommandDefinition(
            "SELECT id, revision, state_id AS stateId FROM work_item WHERE project_id = @projectId AND state_id = ANY(@ids) ORDER BY id",
            new { projectId, ids = targets.Keys.ToArray() }, cancellationToken: cancellationToken));
        return new StateMoves(
            [.. items.Select(i => (i.Id, new WorkItemPatch(i.Revision, new WorkItemChanges { State = new FieldSet<Guid>(targets[i.StateId]) })))],
            targets);
    }

    // An item that entered a removed state after the plan was read is in no patch, so it would reach the catalog's plain-SQL
    // fallback with no revision or event. Lock whatever is left in the removed states and refuse the whole save.
    private static async Task RefuseLateItemsAsync(DbTransaction transaction, StateChanges changes, StateMoves moves, CancellationToken cancellationToken)
    {
        var removed = (changes.Remove ?? []).Select(r => r.StateId).ToArray();
        if (removed.Length == 0)
        {
            return;
        }

        var planned = moves.Patches.Select(p => p.Id).ToArray();
        var late = await transaction.Connection!.QueryAsync<Guid>(new CommandDefinition(
            "SELECT id FROM work_item WHERE state_id = ANY(@removed) AND NOT (id = ANY(@planned)) ORDER BY id FOR UPDATE",
            new { removed, planned }, transaction, cancellationToken: cancellationToken));
        if (late.Any())
        {
            throw ApiException.Conflict("Items changed while you were editing the states; reload and try again. Nothing was saved.");
        }
    }

    private async Task WriteProjectAsync(DbTransaction transaction, Guid projectId, ProjectGeneralPut general, ProjectForecastSettings forecast, CancellationToken cancellationToken)
    {
        var updated = await transaction.Connection!.ExecuteAsync(new CommandDefinition(
            """
            UPDATE project SET name = @name, description = @description, working_days = @days::smallint[],
                               velocity_window = @window, velocity_override = @override, updated_at = @now,
                               agent_connection_id = @agentConnectionId, repo_url = @repoUrl, repo_base_branch = @repoBaseBranch
            WHERE id = @projectId
            """,
            new { projectId, name = general.Name, description = general.Description, days = general.WorkingDays.ToArray(), window = forecast.VelocityWindow, @override = forecast.VelocityOverride,
                general.AgentConnectionId, general.RepoUrl, general.RepoBaseBranch, now = clock.GetUtcNow() },
            transaction, cancellationToken: cancellationToken));
        if (updated == 0)
        {
            throw ApiException.NotFound("There is no such project.");
        }
    }

    // The put is the whole list: a member left out is archived, never deleted, and re-adding revives the row.
    private async Task<IReadOnlyList<Guid>> WriteMembersAsync(DbTransaction transaction, Guid projectId, List<(Guid UserId, ProjectRole Role)> wanted, CancellationToken cancellationToken)
    {
        var connection = transaction.Connection!;
        var now = clock.GetUtcNow();
        var rows = (await connection.QueryAsync<(Guid UserId, string Role, bool Archived)>(new CommandDefinition(
            "SELECT user_id, role, archived_at IS NOT NULL AS archived FROM project_member WHERE project_id = @projectId",
            new { projectId }, transaction, cancellationToken: cancellationToken))).ToDictionary(r => r.UserId);
        foreach (var (userId, role) in wanted)
        {
            var text = role.ToString().ToLowerInvariant();
            if (!rows.TryGetValue(userId, out var row))
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    "INSERT INTO project_member (id, project_id, user_id, role, created_at, updated_at) VALUES (@id, @projectId, @userId, @role, @now, @now)",
                    new { id = Guid.CreateVersion7(), projectId, userId, role = text, now }, transaction, cancellationToken: cancellationToken));
            }
            else if (row.Archived || row.Role != text)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    "UPDATE project_member SET role = @role, archived_at = NULL, updated_at = @now WHERE project_id = @projectId AND user_id = @userId",
                    new { projectId, userId, role = text, now }, transaction, cancellationToken: cancellationToken));
            }
        }

        var keep = wanted.Select(w => w.UserId).ToHashSet();
        var removed = rows.Values.Where(r => !r.Archived && !keep.Contains(r.UserId)).Select(r => r.UserId).ToArray();
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE project_member SET archived_at = @now, updated_at = @now WHERE project_id = @projectId AND user_id = ANY(@removed)",
            new { projectId, removed, now }, transaction, cancellationToken: cancellationToken));
        return removed;
    }
}
