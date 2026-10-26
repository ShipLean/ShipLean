namespace ShipLean.Application.Features.WorkItemDetails.Attachments;

internal static class AttachmentNames
{
    // Only the last path segment is kept, so a name never carries a directory for an agent or a tool to build a path from.
    public static string Clean(string name) => name[(name.LastIndexOfAny(['/', '\\']) + 1)..].Trim();
}
