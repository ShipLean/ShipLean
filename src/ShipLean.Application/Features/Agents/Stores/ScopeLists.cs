namespace ShipLean.Application.Features.Agents.Stores;

// Scopes are kept as a sorted, distinct text[].
public static class ScopeLists
{
    public static IReadOnlyList<string> Sorted(IReadOnlyList<string> scopes) => scopes.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
}
