using System.Buffers.Binary;

namespace ShipLean.Application.Features.Profile;

// What a photo is, decided by its bytes and never by the declared type or the file name: the first bytes name the format and
// the header the format puts first gives the size the image claims, so a file that is truncated, or that would decode to a
// huge bitmap in every viewer's browser, is refused before it is stored.
public static class PhotoFormat
{
    public const int HeaderBytes = 12;

    // The photo is shown at 20 to 96 px; a side above this is never a photo and costs every viewer memory to decode.
    public const int MaxSide = 4096;

    public static readonly IReadOnlyList<string> DeclarableTypes = ["image/png", "image/jpeg", "image/webp"];

    private const int PngHeaderBytes = 24;
    private const int WebPHeaderBytes = 30;

    // The content type the bytes prove, or null when they are none of PNG, JPEG or WebP.
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

        // RIFF <4-byte size> WEBP
        return head.Length >= HeaderBytes && head[..4].SequenceEqual("RIFF"u8) && head.Slice(8, 4).SequenceEqual("WEBP"u8) ? "image/webp" : null;
    }

    // The width and height the header of an image of the given type claims; null when the header is missing or truncated.
    public static (int Width, int Height)? Dimensions(string contentType, ReadOnlySpan<byte> bytes)
        => contentType switch
        {
            "image/png" => PngDimensions(bytes),
            "image/jpeg" => JpegDimensions(bytes),
            "image/webp" => WebPDimensions(bytes),
            _ => null,
        };

    // The IHDR chunk comes first: length (8..11), "IHDR" (12..15), width (16..19), height (20..23).
    private static (int, int)? PngDimensions(ReadOnlySpan<byte> bytes)
        => bytes.Length >= PngHeaderBytes && bytes.Slice(12, 4).SequenceEqual("IHDR"u8)
            ? Sized(BinaryPrimitives.ReadUInt32BigEndian(bytes[16..]), BinaryPrimitives.ReadUInt32BigEndian(bytes[20..]))
            : null;

    // Walks the marker segments to the first start-of-frame (SOF0-SOF15 except DHT, JPG and DAC): height at +5, width at +7.
    private static (int, int)? JpegDimensions(ReadOnlySpan<byte> bytes)
    {
        var at = 2;
        while (at + 4 <= bytes.Length)
        {
            if (bytes[at] != 0xFF)
            {
                return null;
            }

            var marker = bytes[at + 1];
            if (marker == 0xFF)
            {
                at++;
                continue;
            }

            if (marker is 0x01 or (>= 0xD0 and <= 0xD8))
            {
                at += 2;
                continue;
            }

            // End of image and start of scan come before any frame header in a file that hides one. FF 00 (a stuffed byte) and the
            // reserved markers 02 to BF are not segments: a decoder skips them as garbage and keeps scanning, so reading them by
            // their "length" would land the walker on a decoy frame header while the viewer decodes the real one.
            if (marker is 0x00 or (>= 0x02 and <= 0xBF) or 0xD9 or 0xDA)
            {
                return null;
            }

            if (marker is >= 0xC0 and <= 0xCF and not (0xC4 or 0xC8 or 0xCC))
            {
                return at + 9 <= bytes.Length
                    ? Sized(BinaryPrimitives.ReadUInt16BigEndian(bytes[(at + 7)..]), BinaryPrimitives.ReadUInt16BigEndian(bytes[(at + 5)..]))
                    : null;
            }

            var length = BinaryPrimitives.ReadUInt16BigEndian(bytes[(at + 2)..]);
            if (length < 2)
            {
                return null;
            }

            at += 2 + length;
        }

        return null;
    }

    // The first chunk (at 12) is VP8 (lossy), VP8L (lossless) or VP8X (extended); each keeps its size in a different place.
    private static (int, int)? WebPDimensions(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < WebPHeaderBytes)
        {
            return null;
        }

        var chunk = bytes.Slice(12, 4);
        if (chunk.SequenceEqual("VP8 "u8))
        {
            // frame tag (20..22), start code 9D 01 2A (23..25), then 14-bit width (26) and height (28)
            return bytes.Slice(23, 3).SequenceEqual(new byte[] { 0x9D, 0x01, 0x2A })
                ? Sized(BinaryPrimitives.ReadUInt16LittleEndian(bytes[26..]) & 0x3FFF, BinaryPrimitives.ReadUInt16LittleEndian(bytes[28..]) & 0x3FFF)
                : null;
        }

        if (chunk.SequenceEqual("VP8L"u8))
        {
            // signature 0x2F (20), then 14-bit width-1 and 14-bit height-1 packed little-endian (21..24)
            if (bytes[20] != 0x2F)
            {
                return null;
            }

            var packed = BinaryPrimitives.ReadUInt32LittleEndian(bytes[21..]);
            return Sized((packed & 0x3FFF) + 1, ((packed >> 14) & 0x3FFF) + 1);
        }

        // VP8X: flags (20), reserved (21..23), 24-bit width-1 (24..26) and height-1 (27..29)
        return chunk.SequenceEqual("VP8X"u8)
            ? Sized((uint)(bytes[24] | (bytes[25] << 8) | (bytes[26] << 16)) + 1, (uint)(bytes[27] | (bytes[28] << 8) | (bytes[29] << 16)) + 1)
            : null;
    }

    // A size that does not fit an int cannot be small; it is kept as int.MaxValue so the caller's limit refuses it.
    private static (int, int) Sized(uint width, uint height) => ((int)Math.Min(width, int.MaxValue), (int)Math.Min(height, int.MaxValue));

    private static (int, int) Sized(int width, int height) => (width, height);
}
