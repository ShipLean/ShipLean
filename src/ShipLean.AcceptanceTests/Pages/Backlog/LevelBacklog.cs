using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Backlog;

// The Features or Epics backlog: the side-menu level links, the rows and their roll-up cells.
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

    public async Task OpenAsync(string projectKey, string level)
    {
        await page.GotoAsync(Links.Backlog(projectKey, level));
        await Toolbar.WaitForAsync();
        await Rows.First.WaitForAsync();
    }
}
