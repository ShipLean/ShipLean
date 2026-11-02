using Dapper;
using Npgsql;
using ShipLean.Contracts.Analytics;

namespace ShipLean.Application.Features.Analytics;

// Builds the delivery target from the saved settings. Reads the row on every call and caches nothing: the dispatcher caches.
public sealed class AnalyticsSettingsReader(NpgsqlDataSource dataSource) : IAnalyticsSettingsReader
{
    private sealed record Row(string Provider, string? Host, string? SiteKey, bool SendNames, string BaseUrl);

    public async Task<AnalyticsTarget?> GetTargetAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleAsync<Row>(new CommandDefinition(
            "SELECT analytics_provider AS provider, analytics_host AS host, analytics_site_key AS siteKey, analytics_send_names AS sendNames, base_url AS baseUrl FROM instance_settings",
            cancellationToken: cancellationToken));
        var provider = AnalyticsSettings.ParseProvider(row.Provider);
        if (provider == AnalyticsProvider.None || row.Host is null || row.SiteKey is null)
        {
            return null;
        }
        return new AnalyticsTarget(provider, new Uri(row.Host), row.SiteKey, InstanceHost(row.BaseUrl), row.SendNames, ShipLeanVersion.Current);
    }

    // An instance that has not set its base URL stores an empty string; its events then carry no instance host.
    private static string InstanceHost(string baseUrl)
        => Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ? uri.Host : string.Empty;
}
