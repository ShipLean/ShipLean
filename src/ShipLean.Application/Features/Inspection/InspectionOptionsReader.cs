using System.Globalization;
using Microsoft.Extensions.Configuration;
using ShipLean.Application.Features.Inspection.Query;

namespace ShipLean.Application.Features.Inspection;

// Reads the SHIPLEAN_INSPECTION_* variables (K1) into InspectionOptions when first resolved. Every bad value throws a message naming
// the variable, so the app stops at startup (InspectionServices resolves it from a hosted service).
public static class InspectionOptionsReader
{
    public const string Switch = "SHIPLEAN_INSPECTION";
    public const string StoreKey = "SHIPLEAN_INSPECTION_STORE";
    public const string ClickHouse = "clickhouse";
    public const string TempoLoki = "tempo-loki";

    private const string Prefix = "SHIPLEAN_INSPECTION_";

    public static InspectionOptions From(IConfiguration configuration)
    {
        var defaults = new InspectionOptions();
        var fields = InspectionFieldNames.Defaults;
        var options = new InspectionOptions
        {
            Enabled = Enabled(configuration),
            Store = Text(configuration, "STORE")?.ToLowerInvariant(),
            AgentOtlpEndpoint = Text(configuration, "AGENT_OTLP_ENDPOINT"),
            AgentOtlpHeaders = Text(configuration, "AGENT_OTLP_HEADERS"),
            ClickHouseConnection = Text(configuration, "CLICKHOUSE_CONNECTION"),
            ClickHouseDatabase = Identifier(configuration, "CLICKHOUSE_DATABASE", defaults.ClickHouseDatabase),
            ClickHouseTracesTable = Identifier(configuration, "CLICKHOUSE_TRACES_TABLE", defaults.ClickHouseTracesTable),
            ClickHouseLogsTable = Identifier(configuration, "CLICKHOUSE_LOGS_TABLE", defaults.ClickHouseLogsTable),
            RetentionDays = Number(configuration, "CLICKHOUSE_RETENTION_DAYS", defaults.RetentionDays),
            TempoUrl = Text(configuration, "TEMPO_URL"),
            TempoAuth = Auth(configuration, "TEMPO_AUTH"),
            TempoTenant = Text(configuration, "TEMPO_TENANT"),
            LokiUrl = Text(configuration, "LOKI_URL"),
            LokiAuth = Auth(configuration, "LOKI_AUTH"),
            LokiTenant = Text(configuration, "LOKI_TENANT"),
            LokiSelector = Text(configuration, "LOKI_SELECTOR") ?? defaults.LokiSelector,
            Fields = new InspectionFieldNames(
                Text(configuration, "FIELD_ITEM") ?? fields.Item,
                Text(configuration, "FIELD_PROJECT") ?? fields.Project,
                Text(configuration, "FIELD_SESSION") ?? fields.Session,
                Text(configuration, "FIELD_PROMPT") ?? fields.Prompt,
                Text(configuration, "FIELD_REPO") ?? fields.Repo,
                Text(configuration, "FIELD_BRANCH") ?? fields.Branch,
                Text(configuration, "FIELD_PR") ?? fields.Pr),
            MaxSearchDays = Number(configuration, "MAX_SEARCH_DAYS", defaults.MaxSearchDays),
            GrafanaUrlTemplate = Text(configuration, "GRAFANA_URL_TEMPLATE"),
        };
        return options.Enabled ? Validated(options) : options;
    }

    private static InspectionOptions Validated(InspectionOptions options)
    {
        switch (options.Store)
        {
            case null:
                throw new InvalidOperationException($"{StoreKey} must be {ClickHouse} or {TempoLoki} when {Switch}=on.");
            case ClickHouse:
                return options.ClickHouseConnection is null
                    ? throw new InvalidOperationException($"{Prefix}CLICKHOUSE_CONNECTION must be set when {StoreKey}={ClickHouse}.")
                    : options;
            case TempoLoki:
                return options.TempoUrl is null || options.LokiUrl is null
                    ? throw new InvalidOperationException($"{Prefix}TEMPO_URL and {Prefix}LOKI_URL must be set when {StoreKey}={TempoLoki}.")
                    : options;
            default:
                throw new InvalidOperationException($"{StoreKey} must be {ClickHouse} or {TempoLoki}, not \"{options.Store}\".");
        }
    }

    private static bool Enabled(IConfiguration configuration)
        => configuration[Switch]?.Trim().ToLowerInvariant() switch
        {
            null or "" or "off" => false,
            "on" => true,
            var other => throw new InvalidOperationException($"{Switch} must be on or off, not \"{other}\"."),
        };

    private static string? Text(IConfiguration configuration, string name)
        => string.IsNullOrWhiteSpace(configuration[Prefix + name]) ? null : configuration[Prefix + name]!.Trim();

    private static string? Auth(IConfiguration configuration, string name)
    {
        var text = Text(configuration, name);
        return text is null or "none" || text.StartsWith("basic:", StringComparison.Ordinal) || text.StartsWith("bearer:", StringComparison.Ordinal)
            ? text
            : throw new InvalidOperationException($"{Prefix}{name} must be none, basic:<user>:<password> or bearer:<token>.");
    }

    private static string Identifier(IConfiguration configuration, string name, string fallback)
    {
        var text = Text(configuration, name);
        if (text is null)
        {
            return fallback;
        }
        try
        {
            QueryText.Identifier(text);
            return text;
        }
        catch (ArgumentException)
        {
            throw new InvalidOperationException($"{Prefix}{name} must be letters, digits and underscore, not starting with a digit, not \"{text}\".");
        }
    }

    private static int Number(IConfiguration configuration, string name, int fallback)
    {
        var text = Text(configuration, name);
        if (text is null)
        {
            return fallback;
        }
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : throw new InvalidOperationException($"{Prefix}{name} must be a positive whole number, not \"{text}\".");
    }
}
