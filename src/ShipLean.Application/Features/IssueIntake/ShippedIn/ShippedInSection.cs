using System.Globalization;
using System.Text;
using ShipLean.Contracts.IssueIntake;

namespace ShipLean.Application.Features.IssueIntake.ShippedIn;

// The "Shipped in" Markdown section of a GitHub reply (D14), built only from verified facts (ShippedInFacts): for each, the merge
// commit (7 characters) and pull request, when its image was published with the run link (or that it isn't, or that the
// repository has no image workflow), then how a reporter checks their own instance, then the proposal's `requires` conditions.
// With no fact it says ShipLean couldn't confirm the version. The conditions are agent text: the caller runs the whole reply
// through PublicTextGuard before posting it. Pure.
public static class ShippedInSection
{
    public const string Heading = "### Shipped in";

    public const string Unconfirmed = "ShipLean couldn't confirm the version this shipped in.";

    public const string NotYetPublished = "Image: not yet published.";

    public const string NoImageWorkflow = "Image: no image workflow in this repository, so check by commit.";

    public const string PublicationUnknown = "Image: ShipLean couldn't confirm whether it was published.";

    public static string Render(IReadOnlyList<ShippedFact> facts, IReadOnlyList<string> requires)
    {
        var text = new StringBuilder().Append(Heading).Append("\n\n");
        if (facts.Count == 0)
        {
            text.Append(Unconfirmed).Append('\n');
        }
        else
        {
            foreach (var fact in facts)
            {
                text.Append(CultureInfo.InvariantCulture, $"- {fact.Key}: commit `{fact.ShortSha}` ({fact.Repository}#{fact.PullNumber}), merged {Time(fact.MergedAt)}. ")
                    .Append(Image(fact.Publication)).Append('\n');
            }

            text.Append('\n').Append(HowToCheck(facts)).Append('\n');
        }

        var conditions = requires.Select(OneLine).Where(c => c.Length > 0).ToList();
        if (conditions.Count > 0)
        {
            text.Append("\nIt works only when:\n");
            foreach (var condition in conditions)
            {
                text.Append("- ").Append(condition).Append('\n');
            }
        }

        return text.ToString().TrimEnd('\n');
    }

    private static string Image(ImagePublication publication)
    {
        switch (publication)
        {
            case ImagePublication.Published published:
                return $"Image published {Time(published.At)} (run: {published.RunUrl}).";
            case ImagePublication.Failed failed:
                return $"Image: not yet published; the publish run failed ({failed.RunUrl}).";
            case ImagePublication.Pending:
                return NotYetPublished;
            case ImagePublication.NotApplicable:
                return NoImageWorkflow;
            default:
                return PublicationUnknown;
        }
    }

    private static string HowToCheck(IReadOnlyList<ShippedFact> facts)
    {
        var fix = facts[^1].ShortSha;
        var check = new StringBuilder()
            .Append("How to check yours: open `<your instance>/health`; it shows `version: 1.0.0+<sha>`. ")
            .Append(CultureInfo.InvariantCulture, $"Your build has the fix when `git merge-base --is-ancestor {fix} <sha>` succeeds in a clone of {facts[^1].Repository}");
        var published = facts.Select(f => f.Publication).OfType<ImagePublication.Published>().Select(p => p.At).DefaultIfEmpty().Max();
        if (published != default)
        {
            check.Append(CultureInfo.InvariantCulture, $", or when your `:latest` image was pulled after {Time(published)}");
        }

        return check.Append('.').ToString();
    }

    private static string Time(DateTimeOffset at) => at.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);

    // A condition stays one list item: line breaks can't start a heading or a new block.
    private static string OneLine(string condition) => string.Join(' ', condition.Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries)).Trim();
}
