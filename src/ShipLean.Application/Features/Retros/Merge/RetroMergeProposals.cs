using Dapper;
using Npgsql;
using ShipLean.Application.Features.Retros.People;
using ShipLean.Application.Identity;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Retros;

namespace ShipLean.Application.Features.Retros.Merge;

// Agents propose merging one theme into another; a person answers. Merge moves the points through RetroThemeWriter.MergeAsync and only then
// marks the proposal merged, so a failed merge leaves it pending. Keep apart marks it keptApart. The row is never deleted, so the pair
// stays in the unique pair index and cannot be proposed again. Only a pending proposal can be answered; any other is a conflict.
// A singleton, because the insights reader (a singleton) lists the pending ones; so it makes its own RetroThemeWriter, which is only scoped by habit.
internal sealed class RetroMergeProposals(NpgsqlDataSource dataSource, IProjectEvents events) : IRetroMergeProposals
{
    private readonly RetroThemeWriter _themes = new(dataSource, events);

    private const string PendingSql =
        $"""
        SELECT p.id, p.reason, p.proposed_agent_name AS agentName,
               f.id AS fromId, f.name AS fromName, f.tone AS fromTone,
               t.id AS intoId, t.name AS intoName, t.tone AS intoTone,
               (SELECT count(*)::int FROM retro_point rp WHERE rp.theme_id = f.id AND rp.archived_at IS NULL) AS pointCount,
               u.id AS userId, u.name AS userName, u.initials AS userInitials, u.avatar_colour AS userColour, {UserRefs.PhotoUrlSql} AS userPhotoUrl
        FROM retro_theme_merge_proposal p
        JOIN retro_theme f ON f.id = p.from_theme_id
        JOIN retro_theme t ON t.id = p.into_theme_id
        JOIN app_user u ON u.id = p.proposed_by_user_id
        WHERE p.project_id = @projectId AND p.status = 'pending' AND p.archived_at IS NULL
          AND f.archived_at IS NULL AND f.merged_into_id IS NULL
          AND t.archived_at IS NULL AND t.merged_into_id IS NULL
        ORDER BY p.created_at, p.id
        """;

    public async Task<IReadOnlyList<RetroMergeProposalDto>> PendingAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<PendingRow>(new CommandDefinition(PendingSql, new { projectId }, cancellationToken: cancellationToken));
        return [.. rows.Select(row => new RetroMergeProposalDto(
            row.Id,
            new RetroThemeRefDto(row.FromId, row.FromName, Enum.Parse<RetroTone>(row.FromTone, ignoreCase: true)),
            new RetroThemeRefDto(row.IntoId, row.IntoName, Enum.Parse<RetroTone>(row.IntoTone, ignoreCase: true)),
            row.Reason,
            row.PointCount,
            new RetroAuthorDto(new UserRef(row.UserId, row.UserName, row.UserInitials, row.UserColour, row.UserPhotoUrl), row.AgentName, row.AgentName is null ? RetroRole.Person : RetroRole.Conductor)))];
    }

    public async Task MergeAsync(Guid proposalId, RetroActor actor, CancellationToken cancellationToken = default)
    {
        var proposal = await RequirePendingAsync(proposalId, cancellationToken);
        await _themes.MergeAsync(proposal.ProjectId, new RetroThemeMerge([proposal.FromThemeId], proposal.IntoThemeId), cancellationToken);
        await DecideAsync(proposal, "merged", actor, cancellationToken);
    }

    public async Task KeepApartAsync(Guid proposalId, RetroActor actor, CancellationToken cancellationToken = default)
    {
        var proposal = await RequirePendingAsync(proposalId, cancellationToken);
        await DecideAsync(proposal, "keptApart", actor, cancellationToken);
        await events.PublishAsync(new RetroChanged(proposal.ProjectId, [], []));
    }

    private async Task<ProposalRow> RequirePendingAsync(Guid proposalId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<ProposalRow>(new CommandDefinition(
            "SELECT id, project_id AS projectId, from_theme_id AS fromThemeId, into_theme_id AS intoThemeId, status FROM retro_theme_merge_proposal WHERE id = @proposalId AND archived_at IS NULL",
            new { proposalId },
            cancellationToken: cancellationToken))
            ?? throw ApiException.NotFound("No merge proposal with that id was found.");
        return row.Status == "pending" ? row : throw ApiException.Conflict("That merge proposal was already answered.");
    }

    // The status test is in the UPDATE, so of two answers racing for one proposal only one lands.
    private async Task DecideAsync(ProposalRow proposal, string status, RetroActor actor, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var updated = await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE retro_theme_merge_proposal SET status = @status, decided_by_user_id = @userId, decided_at = now() WHERE id = @id AND status = 'pending'",
            new { status, userId = actor.UserId, id = proposal.Id },
            cancellationToken: cancellationToken));
        if (updated == 0)
        {
            throw ApiException.Conflict("That merge proposal was already answered.");
        }
    }

    private sealed record ProposalRow(Guid Id, Guid ProjectId, Guid FromThemeId, Guid IntoThemeId, string Status);

    private sealed record PendingRow(
        Guid Id, string Reason, string? AgentName, Guid FromId, string FromName, string FromTone, Guid IntoId, string IntoName, string IntoTone, int PointCount,
        Guid UserId, string UserName, string UserInitials, string UserColour, string? UserPhotoUrl);
}
