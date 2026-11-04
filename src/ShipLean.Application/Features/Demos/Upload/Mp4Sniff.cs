using Microsoft.AspNetCore.WebUtilities;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Demos.Upload;

// K8's video check: the first 12 bytes must carry "ftyp" at offset 4. They are peeked in the stream's own buffer, so the stream handed on
// still starts at byte 0 and nothing is read past what the check needed.
internal static class Mp4Sniff
{
    private const int HeaderLength = 12;
    private const int TypeOffset = 4;
    private const int BufferSize = 16 * 1024;
    private static readonly byte[] FileType = "ftyp"u8.ToArray();

    public static ApiException NotMp4() => ApiException.Validation("The video must be an MP4.", "video");

    public static async Task<Stream> CheckedAsync(Stream body, CancellationToken cancellationToken)
    {
        var buffered = new BufferedReadStream(body, BufferSize);
        if (!await buffered.EnsureBufferedAsync(HeaderLength, cancellationToken)
            || !buffered.BufferedData.AsSpan(TypeOffset, FileType.Length).SequenceEqual(FileType))
        {
            await buffered.DisposeAsync();
            throw NotMp4();
        }

        return buffered;
    }
}
