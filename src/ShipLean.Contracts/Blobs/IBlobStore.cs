namespace ShipLean.Contracts.Blobs;

// Files by key (attachments, profile photos). A key is a relative, slash-separated path such as "attachments/<id>".
// Callers build keys from ids they generated, never from user input. Keys that are empty, rooted, or climb out with ".."
// throw ArgumentException. There is no delete: blobs are kept (archiving hides rows, it never removes bytes).
public interface IBlobStore
{
    // Stores (or replaces) the blob with its content type and returns the number of bytes stored. All or nothing: a content
    // stream that fails leaves the previous blob, if any, untouched, and readers see the old or the new blob whole.
    Task<long> PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken);

    // Null when the key holds nothing.
    Task<BlobContent?> GetAsync(string key, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken);
}
