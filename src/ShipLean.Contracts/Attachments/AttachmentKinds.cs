using ShipLean.Contracts.Attachments;

namespace ShipLean.Application.Features.WorkItemDetails.Attachments;

// Which AttachmentKind a file is, from its extension or content type.
internal static class AttachmentKinds
{
    private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".gif", ".webp", ".svg"];

    internal static readonly string[] ImageTypes = ["image/png", "image/jpeg", "image/gif", "image/webp", "image/svg+xml"];

    public static AttachmentKind Of(string name, string contentType)
    {
        var extension = Path.GetExtension(name);
        var mediaType = contentType.Split(';')[0].Trim();
        if (extension.Equals(".md", StringComparison.OrdinalIgnoreCase) || mediaType.Equals("text/markdown", StringComparison.OrdinalIgnoreCase))
        {
            return AttachmentKind.Markdown;
        }

        if (ImageExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase) || ImageTypes.Contains(mediaType, StringComparer.OrdinalIgnoreCase))
        {
            return AttachmentKind.Image;
        }

        return AttachmentKind.File;
    }
}
