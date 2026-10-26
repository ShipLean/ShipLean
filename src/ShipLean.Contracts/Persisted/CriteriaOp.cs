using System.Text.Json.Serialization;

namespace ShipLean.Contracts.Persisted;

public enum CriteriaOp
{
    [JsonStringEnumMemberName("=")] Equal,
    [JsonStringEnumMemberName("<>")] NotEqual,
    [JsonStringEnumMemberName(">")] GreaterThan,
    [JsonStringEnumMemberName("<")] LessThan,
    In,
    Contains,
    NotContains,
    IsEmpty,
    IsNotEmpty,
}
