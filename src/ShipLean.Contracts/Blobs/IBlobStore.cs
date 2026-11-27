namespace ShipLean.Contracts.Blobs;

// Files by key (attachments, profile photos). A key is a relative, slash-separated path such as "attachments/<id>".
// Callers build keys from ids they generated, never from user input. Keys that are empty, rooted, or climb out with ".."
// throw ArgumentException. Blobs are normally kept (archiving hides rows, it never removes bytes); the one caller of DeleteAsync is
// ArchivedVideoSweeper, which removes the bytes of archived videos (SHIP-2338).
public interface IBlobStore
{
    // Stores (or replaces) the blob with its content type and returns the number of bytes stored. All or nothing: a content
    // stream that fails leaves the previous blob, if any, untouched, and readers see the old or the new blob whole.
    Task<long> PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken);

    // Null when the key holds nothing.
    Task<BlobContent?> GetAsync(string key, CancellationToken cancellationToken);

    // The blob's content from offset to its end; null when the key holds nothing, decided before the method returns. The caller
    // owns and disposes the stream, and disposing it releases everything behind it.
    Task<Stream?> OpenAtAsync(string key, long offset, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken);

    // Removes the blob. A key that holds nothing is not an error.
    Task DeleteAsync(string key, CancellationToken cancellationToken);
}
