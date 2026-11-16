using Agoda.IoC.Core;
using Dapper;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Inspection.Settings;

// inspection_project_settings by project key. A project with no row has Inspection off.
[RegisterPerRequest]
public sealed class InspectionProjectStore(NpgsqlDataSource dataSource, IProjectAccess access, TimeProvider time)
{
    public sealed record ProjectSettings(Guid ProjectId, string ProjectKey, bool Enabled, bool ShowContent, string? TenantOverride, string? SelectorOverride);

    // Access first: a non-member gets not_found for an existing and a missing project alike.
    public async Task<ProjectSettings> GetAsync(string projectKey, ProjectRole minimum, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        var projectId = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition("SELECT id FROM project WHERE key = @projectKey", new { projectKey }, cancellationToken: ct))
            ?? throw ApiException.NotFound("Project not found.");
        await access.RequireAsync(projectId, minimum, ct);
        var row = await connection.QuerySingleOrDefaultAsync<SettingsRow>(new CommandDefinition(
            """
            SELECT enabled, show_content AS showContent, tenant_override AS tenantOverride, selector_override AS selectorOverride
            FROM inspection_project_settings WHERE project_id = @projectId
            """,
            new { projectId }, cancellationToken: ct));
        return row is null
            ? new ProjectSettings(projectId, projectKey, false, false, null, null)
            : new ProjectSettings(projectId, projectKey, row.Enabled, row.ShowContent, row.TenantOverride, row.SelectorOverride);
    }

    // The key of a project the caller is a member of; not_found otherwise (access first).
    public async Task<string> KeyOfAsync(Guid projectId, CancellationToken ct)
    {
        await access.RequireAsync(projectId, ProjectRole.Member, ct);
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        return await connection.ExecuteScalarAsync<string>(new CommandDefinition("SELECT key FROM project WHERE id = @projectId", new { projectId }, cancellationToken: ct))
            ?? throw ApiException.NotFound("Project not found.");
    }

    public async Task SaveAsync(ProjectSettings settings, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO inspection_project_settings (project_id, enabled, show_content, tenant_override, selector_override, updated_at)
            VALUES (@ProjectId, @Enabled, @ShowContent, @TenantOverride, @SelectorOverride, @now)
            ON CONFLICT (project_id) DO UPDATE SET enabled = @Enabled, show_content = @ShowContent, tenant_override = @TenantOverride,
                selector_override = @SelectorOverride, updated_at = @now
            """,
            new { settings.ProjectId, settings.Enabled, settings.ShowContent, settings.TenantOverride, settings.SelectorOverride, now = time.GetUtcNow() },
            cancellationToken: ct));
    }

    private sealed record SettingsRow(bool Enabled, bool ShowContent, string? TenantOverride, string? SelectorOverride);
}
