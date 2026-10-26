using System.Text.Json;
using ShipLean.Contracts.Criteria;

namespace ShipLean.TestSupport.Contracts.Criteria;

// A case's item as an IFieldValues: a field the item does not list has no value (an undefined element).
public sealed class JsonFieldValues(JsonElement item) : IFieldValues
{
    public JsonElement Get(string fieldId) => item.TryGetProperty(fieldId, out var value) ? value : default;
}
