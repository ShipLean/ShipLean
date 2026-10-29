using System.Text.Json.Serialization;
using ShipLean.Contracts.Search;

namespace ShipLean.WebApi.Infrastructure;

// /health and /api/health. PublicOrigin is set only when base_url is blank and a recent agent request arrived on a
// cluster service name, so a healthy install keeps the payload it had.
public sealed record HealthReport(
    string Status,
    string Version,
    HealthSearch Search,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PublicOrigin = null);
