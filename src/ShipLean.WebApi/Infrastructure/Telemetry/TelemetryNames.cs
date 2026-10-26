namespace ShipLean.WebApi.Infrastructure.Telemetry;

// The one name every ActivitySource and Meter in ShipLean uses, so a single AddSource/AddMeter picks them all up.
internal static class TelemetryNames
{
    public const string ActivitySource = "ShipLean";
    public const string Meter = "ShipLean";
}
