using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Agents;

// The consent page an agent's authorize request lands on (/authorize?request=<id>).
public sealed class ConsentScreen(IPage page)
{
    public ILocator Card => page.GetByTestId(AgentsTestIds.ConsentCard);

    public ILocator Client => page.GetByTestId(AgentsTestIds.ConsentClient);

    public ILocator Host => page.GetByTestId(AgentsTestIds.ConsentHost);

    public ILocator RedirectHost => page.GetByTestId(AgentsTestIds.ConsentRedirectHost);

    public ILocator LocalhostWarning => page.GetByTestId(AgentsTestIds.ConsentLocalhostWarning);

    public ILocator ScopeRead => page.GetByTestId(AgentsTestIds.ConsentScopeRead);

    public ILocator ScopeWrite => page.GetByTestId(AgentsTestIds.ConsentScopeWrite);

    public ILocator ScopePlan => page.GetByTestId(AgentsTestIds.ConsentScopePlan);

    public ILocator Allow => page.GetByTestId(AgentsTestIds.ConsentAllow);

    public ILocator Deny => page.GetByTestId(AgentsTestIds.ConsentDeny);

    public static string Path(Guid requestId) => "/authorize" + Harness.Links.Query(("request", requestId.ToString()));

    public async Task OpenAsync(Guid requestId)
    {
        await page.GotoAsync(Path(requestId));
        await Allow.WaitForAsync();
    }

    // Stands in for the agent's callback, which nothing listens on: answers any request to its origin with an empty page.
    public Task AnswerCallbackAsync(string origin) =>
        page.RouteAsync(origin + "/**", route => route.FulfillAsync(new RouteFulfillOptions { Status = 200, ContentType = "text/html", Body = "<html></html>" }));
}
