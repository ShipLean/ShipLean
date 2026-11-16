namespace ShipLean.AcceptanceTests.Harness;

public static partial class Links
{
    // /p/:key/items/:itemKey/changes, as routes.ts builds it. Commit is a work_item_commit id; layout is "split" or null.
    public static string TaskChanges(string projectKey, string itemKey, string? commit = null, string? file = null, string? layout = null)
    {
        var query = new List<string>();
        if (commit is not null)
        {
            query.Add($"commit={Uri.EscapeDataString(commit)}");
        }
        if (file is not null)
        {
            query.Add($"file={Uri.EscapeDataString(file)}");
        }
        if (layout is not null)
        {
            query.Add($"layout={Uri.EscapeDataString(layout)}");
        }
        var path = $"/p/{Segment(projectKey)}/items/{Segment(itemKey)}/changes";
        return query.Count == 0 ? path : $"{path}?{string.Join('&', query)}";
    }
}
