namespace ShipLean.Contracts.Demos;

public interface IDemoSettings
{
    Task<ProjectDemoSettingsDto> GetAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<ProjectDemoSettingsDto> SaveAsync(Guid projectId, ProjectDemoSettingsPut settings, CancellationToken cancellationToken = default);
}
