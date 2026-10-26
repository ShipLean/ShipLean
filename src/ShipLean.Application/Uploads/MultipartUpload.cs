using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Uploads;

// Reading one file out of a multipart upload, the same way for every feature that takes one: the boundary the reader can
// hold, the request cap, the part named "file", and a body that ends early or is not multipart answered as a validation error.
internal static class MultipartUpload
{
    // Kestrel's own refusals (the 413 for a body over the cap) are IOExceptions too and keep their status.
    public static bool IsTruncation(IOException error) => error is not BadHttpRequestException;

    public static ApiException Unreadable() => ApiException.Validation("The upload could not be read as a file.");

    // RFC 2046 allows at most 70 characters; a longer one does not fit the reader's buffer.
    private const int MaxBoundaryLength = 70;

    // The boundary of a multipart/form-data request, or null when the request is none or the boundary is unusable.
    public static string? BoundaryOf(HttpRequest request)
        => MediaTypeHeaderValue.TryParse(request.ContentType, out var type)
            && type.MediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase)
            && HeaderUtilities.RemoveQuotes(type.Boundary).Value is { Length: > 0 and <= MaxBoundaryLength } boundary
                ? boundary
                : null;

    // Caps what Kestrel will read for this request: the file's limit plus the room the multipart framing needs.
    public static void CapBody(HttpRequest request, long limit)
    {
        var cap = request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (cap is { IsReadOnly: false })
        {
            cap.MaxRequestBodySize = limit + MultipartOverheadBytes;
        }
    }

    private const long MultipartOverheadBytes = 64 * 1024;

    // The part named "file" that carries a file name.
    public static bool IsFilePart(MultipartSection section)
        => ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition)
            && disposition.IsFileDisposition()
            && HeaderUtilities.RemoveQuotes(disposition.Name).Value == "file";

    // The next file part, or null when the body has none. Only the reading of the framing is guarded, so a failure of the
    // caller's own work (a full disk) is never reported as a bad upload.
    public static async Task<MultipartSection?> NextFileSectionAsync(MultipartReader reader, CancellationToken cancellationToken)
    {
        try
        {
            while (await reader.ReadNextSectionAsync(cancellationToken) is { } section)
            {
                if (IsFilePart(section))
                {
                    return section;
                }
            }

            return null;
        }
        catch (InvalidDataException)
        {
            throw Unreadable();
        }
        catch (IOException error) when (IsTruncation(error))
        {
            throw Unreadable();
        }
    }

    // The whole part, never past the limit (tooLarge says what to answer when it is).
    public static async Task<byte[]> ReadAllAsync(Stream part, long limit, ApiException tooLarge, CancellationToken cancellationToken)
    {
        try
        {
            using var buffer = new MemoryStream();
            var chunk = new byte[16 * 1024];
            int read;
            while ((read = await part.ReadAsync(chunk, cancellationToken)) > 0)
            {
                if (buffer.Length + read > limit)
                {
                    throw tooLarge;
                }

                await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
            }

            return buffer.ToArray();
        }
        catch (InvalidDataException)
        {
            throw Unreadable();
        }
        catch (IOException error) when (IsTruncation(error))
        {
            throw Unreadable();
        }
    }
}
