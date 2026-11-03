using System.Text.RegularExpressions;
using ShipLean.Contracts.Retros.Decide;

namespace ShipLean.Application.Features.Retros.Decide;

// Open action items whose title is close to a new one. Pure: lowercase word sets without the words of three letters or fewer; a match
// shares at least half of the smaller set. No embeddings and no search index.
public static partial class RetroDuplicates
{
    private const int ShortestWord = 4;

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex Words();

    public static IReadOnlyList<DecideOpenAction> Find(string title, IReadOnlyList<DecideOpenAction> openActions)
    {
        var wanted = WordSet(title);
        if (wanted.Count == 0)
        {
            return [];
        }

        return [.. openActions.Where(action =>
        {
            var other = WordSet(action.Title);
            return other.Count > 0 && wanted.Intersect(other).Count() * 2 >= Math.Min(wanted.Count, other.Count);
        })];
    }

    private static HashSet<string> WordSet(string text)
        => [.. Words().Matches(text.ToLowerInvariant()).Select(match => match.Value).Where(word => word.Length >= ShortestWord)];
}
