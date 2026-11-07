using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Backlog;

// The Features or Epics backlog: the side-menu level links, the List · Tree switch, the rows and their roll-up cells, and the
// tree's group rows and carets.
public sealed class LevelBacklog(IPage page)
{
    public ILocator Toolbar => page.GetByTestId(BacklogTestIds.Toolbar);

    public ILocator Rows => page.GetByTestId(BacklogTestIds.Row);

    public ILocator Level(string name) => page.GetByTestId(ShellTestIds.NavLevel).Filter(new() { HasText = name });

    // The row of a work item, by key (the cells' text runs together, so the rank sits right before the key).
    public ILocator Row(string key) => Rows.Filter(new() { HasTextRegex = new Regex($"{key}(?!\\d)") });

    public ILocator ProgressText(string key) => Row(key).GetByTestId(BacklogTestIds.LevelProgressText);

    public ILocator PointsSum(string key) => Row(key).GetByTestId(BacklogTestIds.LevelPointsSum);

    public ILocator FeatureCount(string key) => Row(key).GetByTestId(BacklogTestIds.LevelFeatureCount);

    // A List · Tree option, by its label.
    public ILocator Mode(string label) => page.GetByTestId(BacklogTestIds.ModeOption).Filter(new() { HasTextRegex = new Regex($"^{label}$") });

    // Every row of the tree: the Features and the group rows (Epics, Features there for what is under them, No parent).
    public ILocator TreeRows => Rows.Or(page.GetByTestId(BacklogTestIds.TreeGroup));

    // The fold caret of a tree row, by key.
    public ILocator Caret(string key) => TreeRows.Filter(new() { HasTextRegex = new Regex($"{key}(?!\\d)") }).GetByTestId(BacklogTestIds.TreeCaret);

    public async Task OpenAsync(string projectKey, string level)
    {
        await page.GotoAsync(Links.Backlog(projectKey, level));
        await Toolbar.WaitForAsync();
        await Rows.First.WaitForAsync();
    }
}
