namespace ShipLean.Application.Features.Links.OpenGraph;

// What a preview image or favicon is, decided by its first bytes and never by the type the server declared: PNG, JPEG, GIF,
// WebP or ICO. Anything else (SVG included, which can carry script) is not stored.
public static class PreviewImageFormat
{
    // Every type Sniff can answer: the only types a preview image or favicon is served as.
    public static readonly IReadOnlySet<string> ContentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "image/png", "image/jpeg", "image/gif", "image/webp", "image/x-icon",
    };

    // The content type the bytes prove, or null when they are none of the raster formats a preview may be.
    public static string? Sniff(ReadOnlySpan<byte> head)
    {
        if (head.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return "image/png";
        }

        if (head.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }))
        {
            return "image/jpeg";
        }

        if (head.StartsWith("GIF87a"u8) || head.StartsWith("GIF89a"u8))
        {
            return "image/gif";
        }

        // RIFF <4-byte size> WEBP
        if (head.Length >= 12 && head[..4].SequenceEqual("RIFF"u8) && head.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return "image/webp";
        }

        // ICONDIR: reserved 0, type 1 (icon), then at least one image.
        return head.Length >= 6 && head[..4].SequenceEqual(new byte[] { 0x00, 0x00, 0x01, 0x00 }) && (head[4] | head[5]) != 0
            ? "image/x-icon"
            : null;
    }
}
