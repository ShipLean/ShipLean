using Agoda.IoC.Core;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Holidays;
using ShipLean.Contracts.Projects;
using ShipLean.Contracts.Sprints;
using ShipLean.Contracts.Users;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.ProjectSettings;

// The settings page as one document: General, Members, Sprints & forecast, States. A save runs in one transaction (the
// work item store's, because moving items out of a removed state goes through IWorkItemStore.PatchManyAsync; everything
// else is its InTransaction hook), and its events go out after the commit. Callers check access first (Member to read,
// Admin to save).
[RegisterPerRequest]
public sealed partial class ProjectSettingsService(
    NpgsqlDataSource dataSource, TimeProvider clock, IProjectEvents events, IWorkItemStore workItems, IStateCatalog states, IUserDirectory users, ICurrentUser currentUser,
    IPublicHolidays holidays, ISprintCapacityRecompute capacity, ILogger<ProjectSettingsService> logger)
{
    public async Task<ProjectSettingsDto> GetAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var project = await ReadProjectAsync(projectId, cancellationToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var members = await connection.QueryAsync<MemberRow>(new CommandDefinition(MemberRow.Select, new { projectId, now = clock.GetUtcNow() }, cancellationToken: cancellationToken));
        return new ProjectSettingsDto(project.General(await ReadAgentOptionsAsync(connection, project.AgentConnectionId, cancellationToken)), [.. members.Select(m => m.ToDto())], project.Forecast, await states.ListAsync(projectId, cancellationToken));
    }

    // A member leaves a project. Throws ApiException.Validation when they are its only admin.
    public async Task LeaveAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "SELECT id FROM project WHERE id = @projectId FOR NO KEY UPDATE", new { projectId }, transaction, cancellationToken: cancellationToken));
            var mine = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
                "SELECT role FROM project_member WHERE project_id = @projectId AND user_id = @userId AND archived_at IS NULL",
                new { projectId, userId = currentUser.Id }, transaction, cancellationToken: cancellationToken))
                ?? throw ApiException.NotFound("There is no such project.");
            var otherAdmins = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT count(*)::int FROM project_member WHERE project_id = @projectId AND user_id <> @userId AND role = 'admin' AND archived_at IS NULL",
                new { projectId, userId = currentUser.Id }, transaction, cancellationToken: cancellationToken));
            if (mine == "admin" && otherAdmins == 0)
            {
                throw ApiException.Validation("You are the only admin. Make someone else an admin before you leave.");
            }

            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE project_member SET archived_at = @now, updated_at = @now WHERE project_id = @projectId AND user_id = @userId AND archived_at IS NULL",
                new { projectId, userId = currentUser.Id, now = clock.GetUtcNow() }, transaction, cancellationToken: cancellationToken));
            await transaction.CommitAsync(cancellationToken);
        }

        await events.PublishAsync(new ProjectChanged(projectId));
    }

    // The connections a project admin can pick, by name; the project's own choice too when it has since been archived.
    private static async Task<IReadOnlyList<AgentConnectionOption>> ReadAgentOptionsAsync(NpgsqlConnection connection, Guid? chosen, CancellationToken cancellationToken)
        => [.. await connection.QueryAsync<AgentConnectionOption>(new CommandDefinition(
            "SELECT id, name, archived_at IS NOT NULL AS archived FROM agent_connection WHERE archived_at IS NULL OR id = @chosen ORDER BY lower(name) COLLATE \"C\", id",
            new { chosen }, cancellationToken: cancellationToken))];

    private async Task<ProjectRow> ReadProjectAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<ProjectRow>(new CommandDefinition(ProjectRow.Select, new { projectId }, cancellationToken: cancellationToken))
            ?? throw ApiException.NotFound("There is no such project.");
    }
}
