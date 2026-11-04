using Dapper;
using Npgsql;
using ShipLean.Application.Features.AgentDispatch;
using ShipLean.Contracts;
using ShipLean.Contracts.Demos;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Demos.Settings;

// A project's demo settings (K16): the stored automatic flag and environment, the live agent check, and the brand and YouTube parts from their own services.
// Automatic can't be turned on while the agent check fails; when the check fails later with automatic already on, the flag stays and the automatic run says why.
public sealed class DemoSettings(
    NpgsqlDataSource dataSource, AgentSessionStarter sessions, IDemoBrand brand, IYouTubeConnections youTube, ICurrentUser currentUser) : IDemoSettings
{
    public const int MaxEnvironmentLength = 4000;
    private const string ReadyMessage = "Ready to make demos.";

    private sealed record Row(bool Automatic, string Environment);

    public async Task<ProjectDemoSettingsDto> GetAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(
            "SELECT automatic, environment FROM project_demo_settings WHERE project_id = @projectId", new { projectId }, cancellationToken: cancellationToken))
            ?? new Row(false, string.Empty);
        return new ProjectDemoSettingsDto(
            row.Automatic,
            row.Environment,
            await CheckAsync(projectId, cancellationToken),
            await brand.ReadAsync(projectId, cancellationToken),
            await youTube.ReadAsync(projectId, cancellationToken));
    }

    public async Task<ProjectDemoSettingsDto> SaveAsync(Guid projectId, ProjectDemoSettingsPut settings, CancellationToken cancellationToken = default)
    {
        var environment = settings.Environment ?? string.Empty;
        if (environment.Length > MaxEnvironmentLength)
        {
            throw ApiException.Validation($"The environment text can be at most {MaxEnvironmentLength} characters.", "environment");
        }

        if (settings.Automatic && await sessions.PlanProjectAsync(projectId, null, cancellationToken) is { } block)
        {
            throw ApiException.Validation(AgentStartMessages.For(block), "automatic");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO project_demo_settings (project_id, automatic, environment, updated_by, updated_at)
            VALUES (@projectId, @automatic, @environment, @userId, now())
            ON CONFLICT (project_id) DO UPDATE
            SET automatic = @automatic, environment = @environment, updated_by = @userId, updated_at = now()
            """,
            new { projectId, automatic = settings.Automatic, environment, userId = currentUser.Id }, cancellationToken: cancellationToken));
        return await GetAsync(projectId, cancellationToken);
    }

    private async Task<DemoAgentCheckDto> CheckAsync(Guid projectId, CancellationToken cancellationToken)
        => await sessions.PlanProjectAsync(projectId, null, cancellationToken) is { } block
            ? new DemoAgentCheckDto(false, block, AgentStartMessages.For(block))
            : new DemoAgentCheckDto(true, null, ReadyMessage);
}
