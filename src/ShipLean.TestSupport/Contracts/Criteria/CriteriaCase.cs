using System.Text.Json;
using ShipLean.Contracts.Persisted;

namespace ShipLean.TestSupport.Contracts.Criteria;

// One entry of criteria-contract-cases.json. Item holds field values by field id; Matches is null for an invalid case.
public sealed record CriteriaCase(string Name, CriteriaJson Criteria, JsonElement Item, bool Valid, bool? Matches);
