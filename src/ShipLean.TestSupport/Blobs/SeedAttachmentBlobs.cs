using System.Text;
using ShipLean.Contracts.Blobs;

namespace ShipLean.TestSupport.Blobs;

// The bytes behind db/seed/0010_attachments.sql and db/seed/0011_feature_editor_links.sql (the seed writes rows only). A test
// that downloads a seeded attachment, or reads a seeded link's preview image, pins a blob store and puts these into it; the
// attachment lengths match the rows' size_bytes.
public static class SeedAttachmentBlobs
{
    public static readonly IReadOnlyDictionary<string, string> Contents = new Dictionary<string, string>
    {
        ["seed/attachments/wallet-scope.txt"] = "Card wallet scope notes.",
        ["seed/attachments/old-notes.txt"] = "Old notes: superseded",
    };

    // The seeded link's preview image (a 1x1 PNG) and favicon (an ICO header: the link image endpoint serves raster types only),
    // under the keys the link row names.
    public const string LinkPreviewImageKey = "seed/links/preview.png";

    public const string LinkFaviconKey = "seed/links/favicon.svg";

    private static readonly byte[] PreviewImage = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGNgYGD4DwABBAEAfbLI3wAAAABJRU5ErkJggg==");

    private static readonly byte[] Favicon = [0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x10, 0x10, 0x00, 0x00, 0x01, 0x00, 0x20, 0x00];

    public static async Task PutAllAsync(IBlobStore store, CancellationToken cancellationToken = default)
    {
        foreach (var (key, text) in Contents)
        {
            await PutAsync(store, key, Encoding.UTF8.GetBytes(text), "text/plain", cancellationToken);
        }

        await PutAsync(store, LinkPreviewImageKey, PreviewImage, "image/png", cancellationToken);
        await PutAsync(store, LinkFaviconKey, Favicon, "image/x-icon", cancellationToken);
    }

    private static async Task PutAsync(IBlobStore store, string key, byte[] bytes, string contentType, CancellationToken cancellationToken)
    {
        await using var content = new MemoryStream(bytes);
        await store.PutAsync(key, content, contentType, cancellationToken);
    }
}
