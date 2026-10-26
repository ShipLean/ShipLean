using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Planning;

// A shape rule the plan breaks without being refused. Field is the argument path, such as stories[2].tasks[0].context_k.
public sealed record PlanWarning(
    [property: JsonPropertyName("field")] string Field,
    [property: JsonPropertyName("message")] string Message);
