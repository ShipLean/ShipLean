namespace ShipLean.Contracts.Links;

public interface IIntegrationSettings
{
    Task<IntegrationSettingsDto> GetAsync(CancellationToken cancellationToken = default);
}
