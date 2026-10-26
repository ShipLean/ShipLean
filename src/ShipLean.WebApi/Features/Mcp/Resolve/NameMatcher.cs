using ShipLean.Contracts.Errors;

namespace ShipLean.WebApi.Features.Mcp.Resolve;

// The one matching rule every name resolver shares: trimmed and case-insensitive; an exact match wins over a prefix
// match; several matches at the winning level are ambiguous, listing the candidates' display names, sorted. A name that
// matches an inactive (archived or disabled) row exactly, when no live row matches it exactly, is not_found saying so:
// it never falls through to a prefix match on another row.
internal static class NameMatcher
{
    public static (T Match, bool Exact) Pick<T>(
        IReadOnlyList<T> candidates, IReadOnlyList<T> inactive, string? query, string field, string noun,
        Func<T, IEnumerable<string>> exactKeys, Func<T, IEnumerable<string>> prefixKeys, Func<T, string> display)
    {
        var wanted = (query ?? string.Empty).Trim();
        if (wanted.Length == 0)
        {
            throw ApiException.Validation($"Say which {noun} you mean.", field);
        }

        var exact = candidates.Where(c => exactKeys(c).Any(k => Same(k, wanted))).ToList();
        if (exact.Count > 0)
        {
            return (Single(exact, wanted, field, noun, display), true);
        }

        if (inactive.FirstOrDefault(c => exactKeys(c).Any(k => Same(k, wanted))) is { } gone)
        {
            throw ApiException.NotFound($"{display(gone)} is archived.");
        }

        var prefix = candidates.Where(c => prefixKeys(c).Any(k => k.StartsWith(wanted, StringComparison.OrdinalIgnoreCase))).ToList();
        if (prefix.Count > 0)
        {
            return (Single(prefix, wanted, field, noun, display), false);
        }

        throw ApiException.NotFound($"No {noun} matches \"{wanted}\".");
    }

    public static bool Same(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<string> Sorted(IEnumerable<string> names) => [.. names.Distinct().Order(StringComparer.OrdinalIgnoreCase)];

    private static T Single<T>(List<T> matches, string wanted, string field, string noun, Func<T, string> display)
        => matches.Count == 1
            ? matches[0]
            : throw ApiException.Ambiguous($"More than one {noun} matches \"{wanted}\". Say which one.", field, Sorted(matches.Select(display)));
}
