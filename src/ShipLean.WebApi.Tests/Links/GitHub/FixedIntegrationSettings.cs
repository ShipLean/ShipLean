using ShipLean.Contracts.Links;

namespace ShipLean.WebApi.Tests.Links.GitHub;

// IIntegrationSettings that answers one fixed value, for tests of the link sources.
public sealed class FixedIntegrationSettings(IntegrationSettingsDto value) : IIntegrationSettings
{
    public Task<IntegrationSettingsDto> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(value);
}
