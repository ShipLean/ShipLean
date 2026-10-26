using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Import;

// Step 5 of the Jira import stepper: Start, the progress card, the errors and the log.
public sealed class ImportRunPage(IPage page)
{
    // The longest an import of the stub's ten issues may take to reach a finished state.
    public const float FinishDeadlineMs = 60_000;

    public ILocator Step => page.GetByTestId(ImportTestIds.RunStep);

    public ILocator Start => page.GetByTestId(ImportTestIds.RunStart);

    public ILocator Chip => page.GetByTestId(ImportTestIds.RunChip);

    public ILocator Percent => page.GetByTestId(ImportTestIds.RunPercent);

    public ILocator IssueCount => page.GetByTestId(ImportTestIds.RunCount).Filter(new LocatorFilterOptions { HasText = "Issues" });

    public ILocator LogLines => page.GetByTestId(ImportTestIds.RunLogLine);

    public ILocator ErrorRows => page.GetByTestId(ImportTestIds.RunErrorRow);

    public ILocator PendingErrorRows => ErrorRows.Filter(new LocatorFilterOptions { Has = page.GetByTestId(ImportTestIds.RunErrorRetry) });

    public ILocator Busy => page.GetByTestId(ImportTestIds.RunErrorBusy);

    public ILocator RetryButtons => page.GetByTestId(ImportTestIds.RunErrorRetry);

    public ILocator SkipButtons => page.GetByTestId(ImportTestIds.RunErrorSkip);

    public async Task StartAsync() =>
        await page.RunAndWaitForApiAsync("/api/import/jobs", "POST", () => Start.ClickAsync());

    public async Task RetryFirstAsync() => await RunAndWaitForJobPostAsync("/retry", () => RetryButtons.First.ClickAsync());

    public async Task SkipFirstAsync() => await RunAndWaitForJobPostAsync("/skip", () => SkipButtons.First.ClickAsync());

    private async Task RunAndWaitForJobPostAsync(string action, Func<Task> click) =>
        await page.RunAndWaitForResponseAsync(
            click,
            r => r.Request.Method == "POST" && new Uri(r.Url).AbsolutePath.StartsWith("/api/import/jobs/", StringComparison.Ordinal)
                && new Uri(r.Url).AbsolutePath.EndsWith(action, StringComparison.Ordinal));

    public async Task ExpectFinishedAsync(string status) =>
        await Assertions.Expect(Chip).ToHaveAttributeAsync("data-status", status, new LocatorAssertionsToHaveAttributeOptions { Timeout = FinishDeadlineMs });
}
