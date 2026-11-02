using Dapper;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Analytics;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Usage;

namespace ShipLean.Application.Features.Analytics;

// The analytics columns of the instance_settings row: read, validated and written per request, plus the admin's test send.
public sealed class AnalyticsSettings(
    NpgsqlDataSource dataSource,
    IAnalyticsSettingsReader reader,
    IEnumerable<IAnalyticsSink> sinks,
    ICurrentUser currentUser,
    TimeProvider clock)
{
    private const string TestEventName = "analytics.test";
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    private sealed record Row(string Provider, string? Host, string? SiteKey, bool SendNames);

    public async Task<AnalyticsSettingsDto> GetAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await ReadAsync(connection, cancellationToken);
    }

    public async Task<AnalyticsSettingsDto> SaveAsync(AnalyticsSettingsDto settings, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(settings.Provider))
        {
            throw ApiException.Validation("The provider must be none, umami or posthog.", "provider");
        }
        string? host = null;
        string? siteKey = null;
        if (settings.Provider != AnalyticsProvider.None)
        {
            host = NormaliseHost(settings.Host);
            siteKey = ValidateSiteKey(settings.Provider, settings.SiteKey);
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE instance_settings SET analytics_provider = @provider, analytics_host = @host, analytics_site_key = @siteKey, analytics_send_names = @sendNames, updated_at = @now",
            new { provider = WireName(settings.Provider), host, siteKey, sendNames = settings.SendNames, now = clock.GetUtcNow() },
            cancellationToken: cancellationToken));
        return await ReadAsync(connection, cancellationToken);
    }

    // Sends one analytics.test event straight to the saved provider's sink (never through IUsageEvents). A send failure is a result, not an error.
    public async Task<AnalyticsTestResultDto> TestAsync(CancellationToken cancellationToken)
    {
        var target = await reader.GetTargetAsync(cancellationToken);
        if (target is null)
        {
            return new AnalyticsTestResultDto(false, "Analytics is off.");
        }
        var name = DisplayName(target.Provider);
        var sink = sinks.FirstOrDefault(s => s.Provider == target.Provider);
        if (sink is null)
        {
            return new AnalyticsTestResultDto(false, $"No {name} sender is available.");
        }

        using var deadline = new CancellationTokenSource(TestTimeout, clock);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try
        {
            await sink.SendAsync([BuildTestEvent(target)], target, timeout.Token);
        }
        catch (AnalyticsSendException ex)
        {
            return new AnalyticsTestResultDto(false, ex.Message);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            return new AnalyticsTestResultDto(false, $"{name} didn't answer within {(int)TestTimeout.TotalSeconds} seconds: {ex.Message}");
        }
        return new AnalyticsTestResultDto(true, $"Sent a test event to {name}.");
    }

    public static AnalyticsProvider ParseProvider(string wire) => wire switch
    {
        "umami" => AnalyticsProvider.Umami,
        "posthog" => AnalyticsProvider.PostHog,
        _ => AnalyticsProvider.None,
    };

    private AnalyticsEvent BuildTestEvent(AnalyticsTarget target)
    {
        var actor = UsageActor.ForUser(currentUser.Id);
        var userId = currentUser.Id.ToString();
        var properties = new Dictionary<string, object>
        {
            [AnalyticsProps.Surface] = "web",
            [AnalyticsProps.ActorKind] = "user",
            [AnalyticsProps.UserId] = userId,
            [AnalyticsProps.Outcome] = "ok",
            [AnalyticsProps.ShipLeanVersion] = target.ShipLeanVersion,
            [AnalyticsProps.InstanceHost] = target.InstanceHost,
            [AnalyticsProps.ProjectCount] = 0L,
            [AnalyticsProps.Source] = "api:Analytics.Test",
        };
        var person = new AnalyticsPerson(actor.DistinctId, new Dictionary<string, object>
        {
            [AnalyticsProps.PersonKind] = "user",
            [AnalyticsProps.PersonUserId] = userId,
        });
        return new AnalyticsEvent(TestEventName, actor.DistinctId, "user", null, clock.GetUtcNow(), properties, person);
    }

    private static string DisplayName(AnalyticsProvider provider) => provider == AnalyticsProvider.PostHog ? "PostHog" : "Umami";

    private static string WireName(AnalyticsProvider provider) => provider switch
    {
        AnalyticsProvider.Umami => "umami",
        AnalyticsProvider.PostHog => "posthog",
        _ => "none",
    };

    // An absolute http(s) address with no query, fragment or credentials. A path is kept; the trailing "/" is trimmed.
    private static string NormaliseHost(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            throw ApiException.Validation("The host is required, such as https://umami.example.com.", "host");
        }
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || text.Contains('?', StringComparison.Ordinal)
            || text.Contains('#', StringComparison.Ordinal)
            || uri.Query.Length > 0
            || uri.Fragment.Length > 0
            || uri.UserInfo.Length > 0)
        {
            throw ApiException.Validation("The host must be an http or https address with no query, fragment or credentials, such as https://umami.example.com.", "host");
        }
        return (uri.GetLeftPart(UriPartial.Authority) + uri.AbsolutePath).TrimEnd('/');
    }

    private static string ValidateSiteKey(AnalyticsProvider provider, string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (provider == AnalyticsProvider.Umami && !Guid.TryParse(text, out _))
        {
            throw ApiException.Validation("The Umami site key is the website ID, a GUID.", "siteKey");
        }
        if (provider == AnalyticsProvider.PostHog && (!text.StartsWith("phc_", StringComparison.Ordinal) || text.Length == "phc_".Length))
        {
            throw ApiException.Validation("The PostHog site key is the project API key, starting with phc_.", "siteKey");
        }
        return text;
    }

    private static async Task<AnalyticsSettingsDto> ReadAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        var row = await connection.QuerySingleAsync<Row>(new CommandDefinition(
            "SELECT analytics_provider AS provider, analytics_host AS host, analytics_site_key AS siteKey, analytics_send_names AS sendNames FROM instance_settings",
            cancellationToken: cancellationToken));
        return new AnalyticsSettingsDto(ParseProvider(row.Provider), row.Host, row.SiteKey, row.SendNames);
    }
}
