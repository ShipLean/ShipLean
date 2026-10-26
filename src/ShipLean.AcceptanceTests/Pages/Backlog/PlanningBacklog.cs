using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Backlog;

// The Stories backlog in planning mode: sprint sections above the Backlog, their load, the forecast lines and the velocity override.
public sealed class PlanningBacklog(IPage page)
{
    public ILocator Toolbar => page.GetByTestId(BacklogTestIds.Toolbar);

    public ILocator Sections => page.GetByTestId(BacklogTestIds.PlanningSection);

    public ILocator NewItem => page.GetByTestId(BacklogTestIds.NewItem);

    public ILocator AddRow => page.GetByTestId(BacklogTestIds.AddRow);

    public ILocator ForecastToggle => page.GetByTestId(BacklogTestIds.PlanningForecastToggle);

    public ILocator ForecastLines => page.GetByTestId(BacklogTestIds.PlanningForecastLine);

    public ILocator Velocity => page.GetByTestId(BacklogTestIds.PlanningVelocity);

    public ILocator OverrideButton => page.GetByTestId(BacklogTestIds.PlanningOverrideButton);

    public ILocator OverrideInput => page.GetByTestId(BacklogTestIds.PlanningOverrideInput);

    public ILocator OverrideSave => page.GetByTestId(BacklogTestIds.PlanningOverrideSave);

    // A section by its title ("Sprint 24", "Backlog").
    public ILocator Section(string title) =>
        Sections.Filter(new() { Has = page.GetByTestId(BacklogTestIds.PlanningSectionTitle).Filter(new() { HasTextRegex = new Regex($"^{title}$") }) });

    public ILocator CapacityBar(string title) => Section(title).GetByTestId(ComponentTestIds.CapacityBar);

    public ILocator Committed(string title) => Section(title).GetByTestId(BacklogTestIds.PlanningCommitted);

    public ILocator Over(string title) => Section(title).GetByTestId(BacklogTestIds.PlanningOver);

    public ILocator VelocityChip(string title) => Section(title).GetByTestId(BacklogTestIds.PlanningVelocityChip);

    public async Task OpenAsync(string projectKey)
    {
        await page.GotoAsync(Links.Backlog(projectKey, mode: "planning"));
        await Toolbar.WaitForAsync();
        await Section("Backlog").WaitForAsync();
    }

    // The keys of the rows a forecast line is drawn under, top to bottom.
    public async Task<List<string?>> LineKeysAsync()
    {
        var rows = page.GetByTestId(BacklogTestIds.Row).Filter(new() { Has = ForecastLines });
        var keys = new List<string?>();
        foreach (var row in await rows.AllAsync())
        {
            keys.Add(await row.GetAttributeAsync("data-key"));
        }

        return keys;
    }

    // Sets the velocity override and waits for it to be saved; returns the response.
    public async Task<IResponse> SetOverrideAsync(int velocity)
    {
        await OverrideButton.ClickAsync();
        await OverrideInput.FillAsync(velocity.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return await page.RunAndWaitForApiAsync(
            $"/api/projects/{SeedIds.Projects.Ship}/forecast",
            "PUT",
            () => OverrideSave.ClickAsync());
    }
}
