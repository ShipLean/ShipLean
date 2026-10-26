namespace ShipLean.Application.Features.WorkItemDetails.Attachments;

// Whether a stored file is one a browser may show in place. Only the image types qualify, judged by the stored type and never
// by the file name, so a file called a.png that was uploaded as text/html still downloads. An SVG also has to have been
// sanitised when it was stored (rows from before uploads were sanitised were not), or it downloads too.
public static class AttachmentServing
{
    private const string SvgType = "image/svg+xml";

    public static bool IsInline(string contentType, bool sanitized)
    {
        var mediaType = contentType.Split(';')[0].Trim();
        return AttachmentKinds.ImageTypes.Contains(mediaType, StringComparer.OrdinalIgnoreCase)
            && (sanitized || !mediaType.Equals(SvgType, StringComparison.OrdinalIgnoreCase));
    }
}
