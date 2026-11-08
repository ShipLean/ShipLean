using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Dapper;
using Npgsql;
using ShipLean.Application.Features.IssueIntake.Proposal;
using ShipLean.Contracts.IssueIntake;
using ShipLean.Contracts.Refs;

namespace ShipLean.Application.Features.IssueIntake.Acceptance;

// The reads the acceptance lane needs beyond the stores: the stored proposal, the states it moves items to, the parent and the
// duplicate it may name (same project only), and the public text guard for the project.
public sealed class AcceptanceReads(NpgsqlDataSource dataSource)
{
    private const string AcceptedSql =
        "SELECT id FROM state WHERE project_id = @projectId AND type = @typeName AND category = 'proposed' AND NOT is_intake AND archived_at IS NULL ORDER BY sort_order LIMIT 1";

    private const string RemovedSql =
        "SELECT id FROM state WHERE project_id = @projectId AND type = @typeName AND category = 'removed' AND archived_at IS NULL ORDER BY sort_order LIMIT 1";

    // The stored proposal is the C2 JSON (github_issue.proposal); IssueProposalParser checks it with the same rules as a fresh
    // response, but with no fence around it and no response limit.
    public static bool TryReadProposal(string? json, [NotNullWhen(true)] out IssueProposal? proposal, out string? failure)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            proposal = null;
            failure = "The issue has no stored proposal.";
            return false;
        }

        return IssueProposalParser.TryParseStored(json, out proposal, out failure);
    }

    public static WorkItemTypeId TypeOf(ProposalType type) => type switch
    {
        ProposalType.Feature => WorkItemTypeId.Feature,
        ProposalType.Bug => WorkItemTypeId.Bug,
        _ => WorkItemTypeId.Story,
    };

    // The accepted state: the type's first Proposed state that is not a Triage (intake) state.
    public Task<Guid> AcceptedStateAsync(Guid projectId, WorkItemTypeId type, CancellationToken cancellationToken)
        => StateAsync(projectId, type, AcceptedSql, "Proposed", cancellationToken);

    public Task<Guid> RemovedStateAsync(Guid projectId, WorkItemTypeId type, CancellationToken cancellationToken)
        => StateAsync(projectId, type, RemovedSql, "Removed", cancellationToken);

    public async Task<bool> IsRemovedAsync(Guid stateId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT category = 'removed' FROM state WHERE id = @stateId", new { stateId }, cancellationToken: cancellationToken));
    }

    // The item a key names in this project, or null: a key of another project (or malformed) never matches.
    public async Task<ProjectItem?> FindItemAsync(Guid projectId, string projectKey, string? key, CancellationToken cancellationToken)
    {
        var dash = key?.LastIndexOf('-') ?? -1;
        if (key is null || dash <= 0
            || !string.Equals(key[..dash], projectKey, StringComparison.OrdinalIgnoreCase)
            || !int.TryParse(key[(dash + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<ItemRow>(new CommandDefinition(
            "SELECT id, type, title FROM work_item WHERE project_id = @projectId AND number = @number",
            new { projectId, number },
            cancellationToken: cancellationToken));
        return row is null ? null : new ProjectItem(row.Id, $"{projectKey}-{number}", Enum.Parse<WorkItemTypeId>(row.Type, ignoreCase: true), row.Title);
    }

    // The parent the proposal names, only when it is a Feature or Epic of this project that the type allows.
    public async Task<Guid?> ParentAsync(Guid projectId, string projectKey, WorkItemTypeId type, string? parentKey, CancellationToken cancellationToken)
    {
        var parent = await FindItemAsync(projectId, projectKey, parentKey, cancellationToken);
        return parent is { Type: WorkItemTypeId.Feature or WorkItemTypeId.Epic } && AllowsParent(type, parent.Type) ? parent.Id : null;
    }

    // The schema's parent rule (ck_work_item_parent_type_allowed) for the types a proposal can make; Features reach other features
    // only through ShipLean.Contracts, so it is restated here for Feature and Epic parents.
    private static bool AllowsParent(WorkItemTypeId type, WorkItemTypeId parent) => type switch
    {
        WorkItemTypeId.Feature => parent is WorkItemTypeId.Epic or WorkItemTypeId.Feature,
        WorkItemTypeId.Story or WorkItemTypeId.Bug => parent == WorkItemTypeId.Feature,
        _ => false,
    };

    // A guard for one post: the instance's base URL and the names of the project's members.
    public async Task<PublicTextGuard> GuardAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var baseUrls = (await connection.QueryAsync<string>(new CommandDefinition(
            "SELECT base_url FROM instance_settings WHERE base_url <> ''", cancellationToken: cancellationToken))).ToList();
        var names = (await connection.QueryAsync<string>(new CommandDefinition(
            """
            SELECT u.name FROM project_member m JOIN app_user u ON u.id = m.user_id
            WHERE m.project_id = @projectId AND m.archived_at IS NULL
            """,
            new { projectId },
            cancellationToken: cancellationToken))).ToList();
        return new PublicTextGuard(baseUrls, names);
    }

    private async Task<Guid> StateAsync(Guid projectId, WorkItemTypeId type, string sql, string what, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var typeName = type.ToString().ToLowerInvariant();
        return await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            sql,
            new { projectId, typeName },
            cancellationToken: cancellationToken))
            ?? throw new InvalidOperationException($"Project {projectId} has no {what} state for {typeName} items.");
    }

    private sealed record ItemRow(Guid Id, string Type, string Title);
}
