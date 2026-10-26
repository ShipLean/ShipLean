namespace ShipLean.AcceptanceTests.Harness;

public static partial class Links
{
    // A project page with a work item open in the modal: the page's address with ?item=<itemKey>.
    public static string WithItem(string pageLink, string itemKey) =>
        $"{pageLink}{(pageLink.Contains('?') ? '&' : '?')}item={Segment(itemKey)}";
}
