using System.Text.RegularExpressions;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.Webhooks;

// The work item keys a pull or merge request names: in its title (upper case only, so "abc-123" in prose is not a key)
// and in its source branch (any case, upper-cased, because branches are usually lower case). A key in both counts as a
// title match. Whether a key is a real Story, Task or Bug is the caller's lookup.
internal static partial class TicketKeys
{
    public static IReadOnlyList<(string Key, AutoLinkMatch MatchedOn)> In(string title, string sourceBranch)
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
        return found;
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
