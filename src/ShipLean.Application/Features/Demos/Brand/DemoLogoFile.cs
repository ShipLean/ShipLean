using ShipLean.Application.Features.Profile;
using ShipLean.Application.Features.WorkItemDetails.Attachments;

namespace ShipLean.Application.Features.Demos.Brand;

// What a project's logo is, decided by its bytes alone and never by the declared type or the file name (K18). Pure: no I/O.
// A logo is untrusted input that reaches an agent's browser, so only a PNG, JPEG or WebP whose header says it is a sane size,
// or an SVG the attachments' sanitiser cleaned, is ever accepted; the cleaned bytes, not the uploaded ones, are what is kept.
internal static class DemoLogoFile
{
    public const long MaxBytes = 1024 * 1024;

    public const string SvgType = "image/svg+xml";

    public const string NotAnImage = "A logo must be a PNG, JPEG, WebP or SVG image.";

    public const string Damaged = "That logo is damaged or cut short.";

    public static readonly IReadOnlyList<string> ServableTypes = ["image/png", "image/jpeg", "image/webp", SvgType];

    // The type and the bytes to store, or null with the reason the logo is refused.
    public static (string ContentType, byte[] Bytes)? Decide(byte[] bytes, out string? refusal)
    {
        refusal = null;
        if (PhotoFormat.Sniff(bytes) is { } raster)
        {
            return DecideRaster(raster, bytes, out refusal);
        }

        using var stream = new MemoryStream(bytes);
        if (SvgSanitizer.TrySanitize(stream, out var clean))
        {
            return (SvgType, clean);
        }

        refusal = NotAnImage;
        return null;
    }

    private static (string, byte[])? DecideRaster(string type, byte[] bytes, out string? refusal)
    {
        refusal = null;
        if (PhotoFormat.Dimensions(type, bytes) is not var (width, height))
        {
            refusal = Damaged;
            return null;
        }

        if (width is < 1 or > PhotoFormat.MaxSide || height is < 1 or > PhotoFormat.MaxSide)
        {
            refusal = $"A logo can be at most {PhotoFormat.MaxSide} pixels on a side.";
            return null;
        }

        return (type, bytes);
    }
}
