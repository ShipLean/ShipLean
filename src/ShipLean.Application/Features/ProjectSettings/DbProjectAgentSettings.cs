using Dapper;
using Npgsql;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Projects;

namespace ShipLean.Application.Features.ProjectSettings;

// IProjectAgentSettings over the project row. No access check: dispatch code asks, not a user.
public sealed class DbProjectAgentSettings(NpgsqlDataSource dataSource) : IProjectAgentSettings
{
    private sealed record Row(Guid? AgentConnectionId, string? RepoUrl, string? RepoBaseBranch);

    public async Task<ProjectAgentSettings> GetAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(
            "SELECT agent_connection_id AS agentConnectionId, repo_url AS repoUrl, repo_base_branch AS repoBaseBranch FROM project WHERE id = @projectId",
            new { projectId }, cancellationToken: cancellationToken))
            ?? throw ApiException.NotFound("There is no such project.");
        return new ProjectAgentSettings(row.AgentConnectionId, row.RepoUrl, row.RepoBaseBranch ?? ProjectAgentRules.DefaultBranch);
    }
}
