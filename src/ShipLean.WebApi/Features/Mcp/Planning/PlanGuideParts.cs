using ShipLean.Contracts.Errors;

namespace ShipLean.WebApi.Features.Mcp.Planning;

// The wire names of PlanGuidePart: work, guide, brief, report, plan_reviewer, code_reviewer, fix, lead, retro.
public static class PlanGuideParts
{
    private static readonly Dictionary<string, PlanGuidePart> ByWireName = Enum.GetValues<PlanGuidePart>()
        .ToDictionary(part => part.WireName(), StringComparer.Ordinal);

    extension(PlanGuidePart)
    {
        // Throws Validation (field "part") listing the names when the text is none of them.
        public static PlanGuidePart Parse(string? text)
            => text is not null && ByWireName.TryGetValue(text.Trim(), out var part)
                ? part
                : throw ApiException.Validation($"Unknown part '{text}'. The parts are: {string.Join(", ", ByWireName.Keys)}.", "part");
    }

    public static string WireName(this PlanGuidePart part)
        => string.Concat(part.ToString().Select(c => char.IsUpper(c) ? "_" + char.ToLowerInvariant(c) : c.ToString())).TrimStart('_');
}
