namespace ShipLean.AcceptanceTests.Harness;

public static partial class Links
{
    // /p/:key/items/:itemKey, as routes.ts builds it.
    public static string WorkItem(string projectKey, string itemKey) =>
        $"/p/{Segment(projectKey)}/items/{Segment(itemKey)}";
}
