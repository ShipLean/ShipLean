namespace ShipLean.Application.Storage;

// The key rules every IBlobStore applies (IBlobStore): a relative, slash-separated path with no empty, "." or ".."
// segment, no backslash or NUL, and no ".tmp" ending (LocalDiskBlobStore's temporary files use it).
public static class BlobKey
{
    public const string TempSuffix = ".tmp";

    public static void Check(string key)
    {
        if (string.IsNullOrWhiteSpace(key)
            || key.Contains('\\', StringComparison.Ordinal)
            || key.Contains('\0', StringComparison.Ordinal)
            || key.StartsWith('/')
            || key.EndsWith(TempSuffix, StringComparison.Ordinal)
            || key.Split('/').Any(segment => segment is "" or "." or ".."))
        {
            throw new ArgumentException($"'{key}' is not a valid blob key.", nameof(key));
        }
    }
}
