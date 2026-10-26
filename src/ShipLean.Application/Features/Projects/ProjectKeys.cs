using System.Text.RegularExpressions;

namespace ShipLean.Application.Features.Projects;

internal static partial class ProjectKeys
{
    public static bool IsValid(string? key) => key is not null && KeyPattern().IsMatch(key);

    // Keys worth offering when key is taken: the name's initials and prefixes, then the key with its last letter swapped.
    public static IEnumerable<string> Candidates(string key, string name)
    {
        var letters = new string([.. (name ?? string.Empty).ToUpperInvariant().Where(c => c is >= 'A' and <= 'Z')]);
        var initials = new string([.. (name ?? string.Empty).ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(w => w.FirstOrDefault(c => c is >= 'A' and <= 'Z')).Where(c => c != default)]);
        foreach (var source in new[] { initials, letters })
        {
            for (var length = Math.Min(4, source.Length); length >= 2; length--)
            {
                yield return source[..length];
            }
        }
        foreach (var c in Enumerable.Range('A', 26))
        {
            yield return key[..^1] + (char)c;
        }
    }

    [GeneratedRegex("^[A-Z]{2,4}$")]
    private static partial Regex KeyPattern();
}
