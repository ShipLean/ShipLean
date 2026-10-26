using System.Text.Json;

namespace ShipLean.Contracts.Persisted;

// A null-valued JsonElement. Use it for a null From/To/Value: default(JsonElement) throws when serialised.
public static class JsonNull
{
    public static JsonElement Value { get; } = JsonSerializer.SerializeToElement<object?>(null);
}
