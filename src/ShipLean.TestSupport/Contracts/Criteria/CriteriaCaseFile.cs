using System.Text.Json;
using ShipLean.Contracts.Persisted;

namespace ShipLean.TestSupport.Contracts.Criteria;

// The case list both the C# and the TypeScript evaluators pass. Me is the id "@me" stands for.
public sealed record CriteriaCaseFile(Guid Me, IReadOnlyList<CriteriaCase> Cases)
{
    public static CriteriaCaseFile Read()
    {
        var path = Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "Contracts", "Criteria", "criteria-contract-cases.json");
        return JsonSerializer.Deserialize<CriteriaCaseFile>(File.ReadAllText(path), PersistedJson.Options)!;
    }
}
