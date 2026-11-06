using System.Text.Json;
using ShipLean.Application.Storage;

namespace ShipLean.Application.Features.Backups.Engine;

// One blob with its key and content type, as the whole plaintext of one backup object: "SLBLOB1\n", one line of JSON
// {"key","contentType"} ending in "\n" (at most MaxMetadataBytes), then the content up to the end of the stream. There is
// no length field (BlobContent has none): the end of the object ends the content, and the cipher's final record proves
// nothing was cut off. A damaged envelope throws InvalidDataException; an invalid key or oversized metadata given to
// WriteAsync throws ArgumentException.
public static class BlobEnvelope
{
    public const int MaxMetadataBytes = 4096;

    private const char Newline = '\n';
    private static readonly byte[] Magic = "SLBLOB1\n"u8.ToArray();
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // Writes the envelope and the whole of content to output. Neither stream is disposed; content needn't be seekable.
    public static async Task WriteAsync(Stream output, string key, string contentType, Stream content, CancellationToken cancellationToken)
    {
        BlobKey.Check(key);
        ArgumentNullException.ThrowIfNull(contentType);
        var metadata = JsonSerializer.SerializeToUtf8Bytes(new Metadata(key, contentType), Json);
        if (metadata.Length > MaxMetadataBytes)
        {
            throw new ArgumentException($"The key and content type take {metadata.Length} bytes; the limit is {MaxMetadataBytes}.", nameof(key));
        }

        await output.WriteAsync(Magic, cancellationToken);
        await output.WriteAsync(metadata, cancellationToken);
        await output.WriteAsync(new[] { (byte)Newline }, cancellationToken);
        await content.CopyToAsync(output, cancellationToken);
    }

    // Reads the header from input and returns its key, content type and the rest of input as the content (input itself,
    // positioned at the first content byte, so the caller owns and disposes it).
    public static async Task<(string Key, string ContentType, Stream Content)> ReadAsync(Stream input, CancellationToken cancellationToken = default)
    {
        var magic = new byte[Magic.Length];
        if (await input.ReadAtLeastAsync(magic, magic.Length, throwOnEndOfStream: false, cancellationToken) < magic.Length
            || !magic.AsSpan().SequenceEqual(Magic))
        {
            throw new InvalidDataException("This is not a blob envelope: the SLBLOB1 header is missing.");
        }

        var line = new List<byte>();
        var one = new byte[1];
        while (true)
        {
            if (await input.ReadAsync(one, cancellationToken) == 0)
            {
                throw new InvalidDataException("The blob envelope ends inside its metadata line.");
            }

            if (one[0] == (byte)Newline)
            {
                break;
            }

            if (line.Count == MaxMetadataBytes)
            {
                throw new InvalidDataException($"The blob envelope's metadata is over {MaxMetadataBytes} bytes.");
            }

            line.Add(one[0]);
        }

        var metadata = ParseMetadata(line.ToArray());
        try
        {
            BlobKey.Check(metadata.Key);
        }
        catch (ArgumentException)
        {
            throw new InvalidDataException("The blob envelope holds an invalid blob key.");
        }

        return (metadata.Key, metadata.ContentType, input);
    }

    private static Metadata ParseMetadata(byte[] line)
    {
        try
        {
            var metadata = JsonSerializer.Deserialize<Metadata>(line, Json);
            if (metadata is { Key: not null, ContentType: not null })
            {
                return metadata;
            }
        }
        catch (JsonException)
        {
            // Reported below as a damaged envelope.
        }

        throw new InvalidDataException("The blob envelope's metadata line is not a key and a content type.");
    }

    private sealed record Metadata(string Key, string ContentType);
}
