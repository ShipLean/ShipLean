using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Agents;

// The Connected agents panel on Your profile.
public sealed class ConnectedAgentsScreen(IPage page)
{
    public ILocator Panel => page.GetByTestId(AgentsTestIds.SlotConnectedAgents);

    public ILocator Rows => page.GetByTestId(AgentsTestIds.ConnectedRow);

    public ILocator Empty => page.GetByTestId(AgentsTestIds.ConnectedEmpty);

    public ILocator AddLine => page.GetByTestId(AgentsTestIds.ConnectedAddLine);

    public ILocator Row(string clientName) => Rows.Filter(new LocatorFilterOptions { HasText = clientName });

    public async Task OpenAsync()
    {
        await page.GotoAsync("/profile");
        await Panel.WaitForAsync();
    }
}
