using System.Text.Json;

namespace ShipLean.Contracts.Persisted;

public sealed record FieldChange(string Field, JsonElement From, JsonElement To, string? FromLabel, string? ToLabel);
