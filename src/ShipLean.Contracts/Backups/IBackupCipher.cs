namespace ShipLean.Contracts.Backups;

// Encrypts everything a backup uploads with a passphrase (SLBK1, docs/backup-file-format.md).
public interface IBackupCipher
{
    // A write-only stream: what is written to it reaches output encrypted. Disposing it writes the final chunk; it
    // never disposes output.
    Stream OpenEncryptor(Stream output, string passphrase);

    // Decrypts encrypted into output. A wrong passphrase or a damaged file throws BackupDecryptException. Plaintext can
    // reach output before a later part is found damaged, so callers write to a temporary file and publish it only
    // after this returns.
    Task DecryptAsync(Stream encrypted, Stream output, string passphrase, CancellationToken cancellationToken);
}
