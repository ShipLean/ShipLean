using Dapper;
using Npgsql;
using ShipLean.Contracts.Blobs;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Demos.Brand;

// A project's logo row (project_demo_settings.logo_blob and logo_version) and its blob. Reads are always fresh, so a removed logo stops being served at once.
public sealed class DemoLogoStore(NpgsqlDataSource dataSource, IBlobStore blobs, TimeProvider clock) : IDemoLogoStore
{
    // One row snapshot decides everything: the version in the URL, the blob opened and the type reported all come from it, so a replacement racing this read can't pair an old version with a new blob.
    public async Task<DemoLogoRef?> ReadAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var row = await ReadRowAsync(projectId, cancellationToken);
        var content = await OpenBlobAsync(row.Blob, cancellationToken);
        if (content is null)
        {
            return null;
        }

        await using (content.Content)
        {
            return new DemoLogoRef($"/api/projects/{projectId}/demo-brand/logo?v={row.Version}", content.ContentType);
        }
    }

    public async Task<BlobContent> OpenAsync(Guid projectId, CancellationToken cancellationToken = default)
        => await TryOpenAsync(projectId, cancellationToken) ?? throw ApiException.NotFound("This project has no logo.");

    public async Task<BlobContent?> TryOpenAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var row = await ReadRowAsync(projectId, cancellationToken);
        return await OpenBlobAsync(row.Blob, cancellationToken);
    }

    // The blob a row snapshot names, or null when none is set, it is gone, or its stored type is not one a logo can have.
    private async Task<BlobContent?> OpenBlobAsync(string? blob, CancellationToken cancellationToken)
    {
        var content = blob is null ? null : await blobs.GetAsync(blob, cancellationToken);
        if (content is not null && !DemoLogoFile.ServableTypes.Contains(content.ContentType))
        {
            await content.Content.DisposeAsync();
            return null;
        }

        return content;
    }

    public async Task SetAsync(Guid projectId, string blobKey, Guid userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO project_demo_settings (project_id, logo_blob, logo_version, updated_by, updated_at)
            VALUES (@projectId, @blobKey, 1, @userId, @now)
            ON CONFLICT (project_id) DO UPDATE
            SET logo_blob = @blobKey, logo_version = project_demo_settings.logo_version + 1, updated_by = @userId, updated_at = @now
            """,
            new { projectId, blobKey, userId, now = clock.GetUtcNow() }, cancellationToken: cancellationToken));
    }

    public async Task RemoveAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE project_demo_settings SET logo_blob = NULL, logo_version = logo_version + 1, updated_at = @now WHERE project_id = @projectId AND logo_blob IS NOT NULL",
            new { projectId, now = clock.GetUtcNow() }, cancellationToken: cancellationToken));
    }

    private async Task<(string? Blob, int Version)> ReadRowAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<(string? Blob, int Version)>(new CommandDefinition(
            "SELECT logo_blob AS blob, logo_version AS version FROM project_demo_settings WHERE project_id = @projectId",
            new { projectId }, cancellationToken: cancellationToken));
    }
}
