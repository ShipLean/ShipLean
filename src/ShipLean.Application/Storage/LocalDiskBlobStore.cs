using System.Buffers.Binary;
using System.Text;
using ShipLean.Contracts.Blobs;

namespace ShipLean.Application.Storage;

// Blobs as files under one root folder. Each file holds its content type and its bytes together:
// [int32 little-endian length][UTF-8 content type][bytes]. A put writes the whole file to a temporary name beside it and
// renames it into place in one step, so a failed put leaves the previous blob and a reader (who opens the file once)
// always sees one put's type and bytes together. Temporary files left by a crashed process are named "*.<guid>.tmp".
public sealed class LocalDiskBlobStore : IBlobStore
{
    private const int MaxContentTypeBytes = 1024;

    private readonly string _root;

    public LocalDiskBlobStore(string root)
    {
        _root = Path.GetFullPath(root);
        Directory.CreateDirectory(_root);
    }

    public async Task<long> PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        var path = PathFor(key);
        var type = Encoding.UTF8.GetBytes(contentType);
        if (type.Length > MaxContentTypeBytes)
        {
            throw new ArgumentException("The content type is too long.", nameof(contentType));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = $"{path}.{Guid.NewGuid():N}{BlobKey.TempSuffix}";
        try
        {
            long count = 0;
            await using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                var header = new byte[sizeof(int)];
                BinaryPrimitives.WriteInt32LittleEndian(header, type.Length);
                await file.WriteAsync(header, cancellationToken);
                await file.WriteAsync(type, cancellationToken);
                var buffer = new byte[81920];
                int read;
                while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    count += read;
                }
            }

            File.Move(temp, path, overwrite: true);
            return count;
        }
        catch
        {
            TryDelete(temp);
            throw;
        }
    }

    public async Task<BlobContent?> GetAsync(string key, CancellationToken cancellationToken)
    {
        var path = PathFor(key);
        FileStream file;
        try
        {
            file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, useAsync: true);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }

        try
        {
            var header = new byte[sizeof(int)];
            await file.ReadExactlyAsync(header, cancellationToken);
            var length = BinaryPrimitives.ReadInt32LittleEndian(header);
            if (length is < 0 or > MaxContentTypeBytes)
            {
                throw new InvalidDataException($"Blob '{key}' has no valid header.");
            }

            var type = new byte[length];
            await file.ReadExactlyAsync(type, cancellationToken);
            return new BlobContent(new BlobBodyStream(file, file.Position), Encoding.UTF8.GetString(type));
        }
        catch
        {
            await file.DisposeAsync();
            throw;
        }
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken)
        => Task.FromResult(File.Exists(PathFor(key)));

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // The put's own error is the one to report; a leftover temp file is harmless.
        }
    }

    private string PathFor(string key)
    {
        BlobKey.Check(key);

        var full = Path.GetFullPath(Path.Combine(_root, key));
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException($"'{key}' is not a valid blob key.", nameof(key));
        }

        var current = _root;
        foreach (var segment in key.Split('/'))
        {
            current = Path.Combine(current, segment);
            if (new FileInfo(current).LinkTarget is not null)
            {
                throw new ArgumentException($"'{key}' goes through a symbolic link.", nameof(key));
            }
        }

        return full;
    }
}
