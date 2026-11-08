using Dapper;
using Npgsql;
using ShipLean.Contracts.IssueIntake;

namespace ShipLean.Application.Features.IssueIntake.Settings;

// IIssueIntakeSettings over project_issue_intake: the project whose intake is on for the repository, compared without case.
// No access check: the intake lanes ask, not a user.
public sealed class DbIssueIntakeSettings(NpgsqlDataSource dataSource) : IIssueIntakeSettings
{
    public async Task<IssueIntakeProject?> ForRepositoryAsync(IssueRef issue, CancellationToken cancellationToken = default)
    {
        if (issue.Host != IssueRef.GitHubHost)
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(
            """
            SELECT p.id AS projectId, p.key AS projectKey, i.repository, i.run_as_user_id AS runAsUserId, i.proposal_instructions AS instructions, i.max_rounds AS maxRounds
            FROM project_issue_intake i JOIN project p ON p.id = i.project_id
            WHERE i.enabled AND lower(i.repository) = lower(@repository)
            """,
            new { repository = issue.Repository }, cancellationToken: cancellationToken));
        if (row is null)
        {
            return null;
        }

        var instructions = string.IsNullOrWhiteSpace(row.Instructions) ? IssueIntakeSettingsService.DefaultInstructions : row.Instructions;
        return new IssueIntakeProject(row.ProjectId, row.ProjectKey, row.Repository, row.RunAsUserId, instructions, row.MaxRounds);
    }

    private sealed record Row(Guid ProjectId, string ProjectKey, string Repository, Guid? RunAsUserId, string Instructions, int MaxRounds);
}
