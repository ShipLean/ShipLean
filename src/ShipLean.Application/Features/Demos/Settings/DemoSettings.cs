using ShipLean.Contracts.Demos;

namespace ShipLean.Application.Features.Demos.Settings;

public sealed class DemoSettings(IDemoBrand brand) : IDemoSettings
{
    // TODO: the real settings replace this: the stored automatic flag and environment, the live agent check and the YouTube connection.
    public async Task<ProjectDemoSettingsDto> GetAsync(Guid projectId, CancellationToken cancellationToken = default)
        => new(
            false,
            string.Empty,
            new DemoAgentCheckDto(true, null, string.Empty),
            await brand.ReadAsync(projectId, cancellationToken),
            new YouTubeProjectDto(YouTubeConnectionStatus.Off, null, null, null, null));

    // TODO: saving replaces this; nothing is saved yet.
    public Task<ProjectDemoSettingsDto> SaveAsync(Guid projectId, ProjectDemoSettingsPut settings, CancellationToken cancellationToken = default)
        => GetAsync(projectId, cancellationToken);
}
