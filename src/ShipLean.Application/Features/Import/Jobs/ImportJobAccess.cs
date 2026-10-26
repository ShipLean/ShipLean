using Agoda.IoC.Core;
using Dapper;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Import.Jobs;

// Who may act on an import. Imports belong to project admins: of the project that owns the Jira connection, or of the project the import went into. The import job rule of
// IProjectAccess is not enough, because a job whose new project is not created yet has no project to check.
[RegisterPerRequest]
public sealed class ImportJobAccess(NpgsqlDataSource dataSource, IProjectAccess access)
{
    private sealed record Owners(Guid ConnectionProjectId, Guid? TargetProjectId);

    public async Task RequireJobAsync(Guid jobId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var owners = await connection.QuerySingleOrDefaultAsync<Owners>(new CommandDefinition(
            "SELECT c.project_id AS connectionProjectId, j.project_id AS targetProjectId FROM import_job j JOIN jira_connection c ON c.id = j.connection_id WHERE j.id = @jobId",
            new { jobId }, cancellationToken: cancellationToken)) ?? throw ApiException.NotFound("That import does not exist.");
        try
        {
            await access.RequireAsync(owners.ConnectionProjectId, ProjectRole.Admin, cancellationToken);
        }
        catch (ApiException refused) when (owners.TargetProjectId is { } target)
        {
            try
            {
                await access.RequireAsync(target, ProjectRole.Admin, cancellationToken);
            }
            catch (ApiException)
            {
                throw refused;
            }
        }
    }

    public async Task RequireErrorAsync(Guid jobId, Guid errorId, CancellationToken cancellationToken)
    {
        await RequireJobAsync(jobId, cancellationToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var belongs = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM import_error WHERE id = @errorId AND import_job_id = @jobId)", new { errorId, jobId }, cancellationToken: cancellationToken));
        if (!belongs)
        {
            throw ApiException.NotFound("That error does not exist.");
        }
    }

    public Task RequireConnectionAsync(Guid connectionId, CancellationToken cancellationToken)
        => access.RequireAsync(ProjectItem.JiraConnection, connectionId, ProjectRole.Admin, cancellationToken);
}
