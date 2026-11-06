namespace ShipLean.Contracts.Backups;

// A place backups are written to (an S3 bucket so far). Keys are whole object keys within the destination, built by
// BackupObjectKeys from the configured prefix; the destination adds nothing to them. Every failure is a
// BackupDestinationException, whose message never holds a secret. Made by IBackupDestinationFactory, one per
// provider, never registered itself.
[MultipleImplementations]
public interface IBackupDestination
{
    // Stores (or replaces) the object. The content stream is seekable, so an implementation may read it more than once.
    Task PutAsync(string key, Stream content, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken);

    // Removing a key that holds nothing is not an error. Only backup objects ShipLean wrote are ever deleted (D3).
    Task DeleteAsync(string key, CancellationToken cancellationToken);

    // Every object whose key starts with prefix, with its size in bytes.
    IAsyncEnumerable<BackupObject> ListAsync(string prefix, CancellationToken cancellationToken);

    // Writes, reads back and deletes a small object under the destination's prefix, stopping at the first step that
    // fails. A failure is a result (Ok false, with the step), not an exception.
    Task<BackupProbeResult> ProbeAsync(CancellationToken cancellationToken);
}
