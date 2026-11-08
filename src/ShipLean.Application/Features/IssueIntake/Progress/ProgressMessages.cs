using System.Globalization;

namespace ShipLean.Application.Features.IssueIntake.Progress;

// The progress comments ShipLean posts on an accepted issue (D13, D14), one per milestone. They name only what D17 allows: SHIP
// keys, item titles, sprint names and dates, and public pull request links. The fixed-in section comes from ShippedInSection.
// The caller runs every text through PublicTextGuard before it is enqueued (titles are member-edited). Pure.
public static class ProgressMessages
{
    public const string ClosingCompleted = "ShipLean is closing this issue as completed.";

    public const string ClosingNotPlanned = "ShipLean is closing this issue as not planned.";

    public static string Sprint(string key, string title, string sprintName, string startDate, string endDate)
        => $"{Item(key, title)} is planned for the sprint {OneLine(sprintName)} ({startDate} to {endDate}).";

    public static string Started(string key, string title) => $"Work on {Item(key, title)} has started.";

    public static string PullRequest(string key, string title, string url, bool merged)
        => merged
            ? $"A pull request for {Item(key, title)} was merged: {url}"
            : $"A pull request for {Item(key, title)} is open: {url}";

    public static string ChildDone(string key, string title, string childKey, string childTitle, int done, int total)
        => string.Create(CultureInfo.InvariantCulture, $"{Item(childKey, childTitle)} is done: {done} of {total} done for {Item(key, title)}.");

    public static string Done(string key, string title, string shippedInSection)
        => $"{Item(key, title)} is done.\n\n{shippedInSection}\n\n{ClosingCompleted}";

    public static string Removed(string key, string title) => $"{Item(key, title)} was removed from the plan. {ClosingNotPlanned}";

    private static string Item(string key, string title) => $"{key} \"{OneLine(title)}\"";

    // A title stays on one line: a line break can't start a heading or a new block.
    private static string OneLine(string text) => string.Join(' ', text.Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries)).Trim();
}
