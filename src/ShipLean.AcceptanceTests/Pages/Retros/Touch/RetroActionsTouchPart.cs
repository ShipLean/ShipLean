using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Retros.Touch;

// Retrospectives › Insights, Action items and Changes & impact at 900px and below (SHIP-731): the action item cards, the
// How verdicts work button and the impact bars with their read-out. Use with RetroInsightsPage for the panel and its tabs.
public sealed class RetroActionsTouchPart(IPage page)
{
    public ILocator Cards => page.GetByTestId(RetrosTestIds.RetroActionCards);

    public ILocator Card => page.GetByTestId(RetrosTestIds.RetroActionCard);

    public ILocator CardOf(string text) => Card.Filter(new() { HasText = text });

    public static ILocator LinkOf(ILocator card) => card.GetByTestId(RetrosTestIds.RetroActionCardLink);

    public static ILocator PullOf(ILocator card) => card.GetByTestId(RetrosTestIds.RetroActionPull);

    public ILocator HowVerdicts => page.GetByTestId(RetrosTestIds.RetroImpactHowVerdicts);

    public static ILocator BarsOf(ILocator impactCard) => impactCard.GetByTestId(RetrosTestIds.RetroImpactBar);

    public static ILocator ReadoutOf(ILocator impactCard) => impactCard.GetByTestId(RetrosTestIds.RetroImpactReadout);
}
