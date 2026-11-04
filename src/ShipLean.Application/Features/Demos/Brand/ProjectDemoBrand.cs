using ShipLean.Contracts.Blobs;
using ShipLean.Contracts.Demos;

namespace ShipLean.Application.Features.Demos.Brand;

// Final: the logo goes to DemoLogoStore, the tokens to DemoTokensStore, and the dto is composed from both and the defaults.
public sealed class ProjectDemoBrand(IDemoLogoStore logo, IDemoTokensStore tokenStore) : IDemoBrand
{
    public async Task<ProjectDemoBrandDto> ReadAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var stored = await logo.ReadAsync(projectId, cancellationToken);
        return new ProjectDemoBrandDto(stored?.Url, stored?.ContentType, await tokenStore.ReadAsync(projectId, cancellationToken), DemoBrandDefaults.DefaultTokens);
    }

    public Task<BlobContent> OpenLogoAsync(Guid projectId, CancellationToken cancellationToken = default)
        => logo.OpenAsync(projectId, cancellationToken);

    public async Task<ProjectDemoBrandDto> SaveTokensAsync(Guid projectId, string? tokens, CancellationToken cancellationToken = default)
    {
        await tokenStore.SaveAsync(projectId, tokens, cancellationToken);
        return await ReadAsync(projectId, cancellationToken);
    }

    public async Task<ProjectDemoBrandDto> RemoveLogoAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await logo.RemoveAsync(projectId, cancellationToken);
        return await ReadAsync(projectId, cancellationToken);
    }
}
