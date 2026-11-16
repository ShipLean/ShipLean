using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.InspectionMetrics;

// Inspection (/p/<project>/inspection?view=…, SHIP-1398 K10): the side menu's Inspection section, the page's header and
// view tabs, its loading and error states, and the root of each view. Each view's own parts are found inside its root.
// Frozen after wave 0.1: a view's tests add their own page object over its root instead of editing this one.
public sealed class InspectionMetricsPage(IPage page)
{
    public static string Path(string projectKey, string? view = null)
        => view is null ? $"/p/{Uri.EscapeDataString(projectKey)}/inspection" : $"/p/{Uri.EscapeDataString(projectKey)}/inspection?view={Uri.EscapeDataString(view)}";

    public ILocator Page => page.GetByTestId(InspectionMetricsTestIds.Page);

    // The side menu: the section, then its sub-items in menu order.
    public ILocator MenuItem => page.GetByTestId(InspectionMetricsTestIds.MenuItem);

    public ILocator MenuOverview => page.GetByTestId(InspectionMetricsTestIds.MenuOverview);

    public ILocator MenuLeadTime => page.GetByTestId(InspectionMetricsTestIds.MenuLeadTime);

    public ILocator Header => Page.GetByTestId(InspectionMetricsTestIds.Header);

    public ILocator HeaderTitle => Page.GetByTestId(InspectionMetricsTestIds.HeaderTitle);

    public ILocator Caveat => Page.GetByTestId(InspectionMetricsTestIds.Caveat);

    public ILocator ViewTabs => Page.GetByTestId(InspectionMetricsTestIds.ViewTab);

    // A header tab by its label (Overview, Lead time), never by position. Each tab also carries data-view.
    public ILocator ViewTab(string label) => ViewTabs.Filter(new() { HasTextString = label });

    public ILocator Loading => Page.GetByTestId(InspectionMetricsTestIds.Loading);

    public ILocator Error => Page.GetByTestId(InspectionMetricsTestIds.Error);

    public ILocator ErrorRetry => Page.GetByTestId(InspectionMetricsTestIds.ErrorRetry);

    // The root of each view (the slot the page mounts for ?view=).
    public ILocator Overview => Page.GetByTestId(InspectionMetricsTestIds.Overview);

    public ILocator LeadTime => Page.GetByTestId(InspectionMetricsTestIds.LeadTime);

    public ILocator Period => Page.GetByTestId(InspectionMetricsTestIds.Period);

    public ILocator Json => Page.GetByTestId(InspectionMetricsTestIds.Json);
}
