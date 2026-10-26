using System.Diagnostics;

namespace ShipLean.Core.Telemetry;

// The one ActivitySource for ShipLean's own spans (background jobs, MCP tool calls). Nothing listens unless the host
// exports telemetry, so StartActivity returns null and these calls cost nothing. Its name matches TelemetryNames.ActivitySource.
public static class ShipLeanActivities
{
    public const string SourceName = "ShipLean";

    public static readonly ActivitySource Source = new(SourceName);
}
