using System.Text.Json.Serialization;
using ShipLean.Contracts.Search;

namespace ShipLean.WebApi.Infrastructure;

// /health "search": how the embedder is running. Mode is semantic or lexical (camelCase); Reason says why lexical; Model is name@revision.
public sealed record HealthSearch(
    EmbedderMode Mode,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Reason,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Model);
