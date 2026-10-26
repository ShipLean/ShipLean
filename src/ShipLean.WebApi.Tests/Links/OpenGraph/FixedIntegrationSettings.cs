using ShipLean.Contracts.Links;

namespace ShipLean.WebApi.Tests.Links.OpenGraph;

// Integration settings with only a preview allow-list, as an instance admin would set it. The list can be set after the host
// starts, for entries that name a stub server's port.
public sealed class FixedIntegrationSettings(params string[] previewAllowHosts) : IIntegrationSettings
{
    public IReadOnlyList<string> PreviewAllowHosts { get; set; } = previewAllowHosts;

    public Task<IntegrationSettingsDto> GetAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new IntegrationSettingsDto(null, "https://api.github.invalid", null, null, PreviewAllowHosts));
}
