using System.Text.RegularExpressions;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Backlog;

// The "Before you plan" strip above the Stories list and Planning: the five checks, and the queue bar once one is open.
public sealed class BeforeYouPlanStrip(IPage page)
{
    public const string Unestimated = "not estimated";

    public const string NoCriteria = "no acceptance criteria";

    public const string NoParent = "no parent";

    public const string Stale = "stale";

    public const string Duplicates = "likely duplicates";

    public ILocator Strip => page.GetByTestId(BacklogTestIds.PlanStrip);

    public ILocator Checks => page.GetByTestId(BacklogTestIds.PlanCheck);

    // A check by its label on the strip ("not estimated", "stale").
    public ILocator Check(string label) => Checks.Filter(new() { HasTextRegex = new Regex($"{Regex.Escape(label)}$") });

    public ILocator QueueBar => page.GetByTestId(BacklogTestIds.PlanQueueBar);

    public ILocator QueueExit => page.GetByTestId(BacklogTestIds.PlanQueueExit);

    public ILocator DoneMarks => page.GetByTestId(BacklogTestIds.QueueDone);

    // The five counts as the strip shows them, in order; a cleared check reads 0.
    public async Task<Dictionary<string, int>> CountsAsync()
    {
        var counts = new Dictionary<string, int>();
        foreach (var label in new[] { Unestimated, NoCriteria, NoParent, Stale, Duplicates })
        {
            var text = await Check(label).InnerTextAsync();
            var lead = Regex.Match(text, @"^(\d+|✓)").Groups[1].Value;
            counts[label] = lead == "✓" ? 0 : int.Parse(lead, System.Globalization.CultureInfo.InvariantCulture);
        }

        return counts;
    }
}
