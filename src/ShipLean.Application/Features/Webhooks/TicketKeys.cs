using System.Text.RegularExpressions;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.Webhooks;

// The work item keys a pull or merge request names: in its title (upper case only, so "abc-123" in prose is not a key),
// in its source branch (any case, upper-cased, because branches are usually lower case), and in a "ShipLean IDs:" line
// that opens its body. A key in several counts as the first of title, branch, body. Whether a key is a real Story, Task
// or Bug is the caller's lookup.
internal static partial class TicketKeys
{
    // The most keys read from a body line and the most items one event links: anyone can open a pull request on a public
    // repository, and a body is far longer than a title.
    public const int MaxItems = 100;

    private const string BodyLabel = "ShipLean IDs:";

    public static IReadOnlyList<(string Key, AutoLinkMatch MatchedOn)> In(string title, string sourceBranch, IReadOnlyList<string> bodyKeys)
    {
        var found = new List<(string Key, AutoLinkMatch MatchedOn)>();
        foreach (var key in TitleKey().Matches(title).Select(m => m.Value))
        {
            Add(found, key, AutoLinkMatch.Title);
        }
        foreach (var key in BranchKey().Matches(sourceBranch).Select(m => m.Value.ToUpperInvariant()))
        {
            Add(found, key, AutoLinkMatch.Branch);
        }
        foreach (var key in bodyKeys)
        {
            Add(found, key, AutoLinkMatch.Body);
        }
        return found;
    }

    // Only the first non-blank line counts, so a body that quotes the convention further down names nothing.
    public static IReadOnlyList<string> InBody(string body)
    {
        var text = body.TrimStart();
        var end = text.AsSpan().IndexOfAny('\r', '\n');
        var line = end < 0 ? text : text[..end];
        return line.StartsWith(BodyLabel, StringComparison.OrdinalIgnoreCase)
            ? [.. TitleKey().Matches(line[BodyLabel.Length..]).Select(m => m.Value).Distinct().Take(MaxItems)]
            : [];
    }

    private static void Add(List<(string Key, AutoLinkMatch MatchedOn)> found, string key, AutoLinkMatch matchedOn)
    {
        if (!found.Exists(f => f.Key == key))
        {
            found.Add((key, matchedOn));
        }
    }

    [GeneratedRegex(@"\b[A-Z]{2,4}-\d+\b", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TitleKey();

    [GeneratedRegex(@"\b[A-Z]{2,4}-\d+\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex BranchKey();
}
