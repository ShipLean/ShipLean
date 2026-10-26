using System.Text.Json;

namespace ShipLean.Contracts.Persisted;

public sealed record FieldValueOption(JsonElement Value, string Label);
