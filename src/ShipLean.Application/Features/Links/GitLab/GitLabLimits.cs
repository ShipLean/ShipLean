using System.Text.Json;

namespace ShipLean.Application.Features.Links.GitLab;

// What the GitLab source keeps of a provider answer: a body over MaxBody is refused, and the title and author are cut to the
// lengths below before they are stored (the provider's own limits are about 255 characters, but a host may not keep to them).
public static class GitLabLimits
{
    public const int MaxBody = 1024 * 1024;
    public const int MaxTitle = 255;
    public const int MaxAuthor = 100;

    public static string? Truncate(string? text, int max)
    {
        if (text is null || text.Length <= max)
        {
            return text;
        }

        // Never end on half of a surrogate pair.
        return text[..(char.IsHighSurrogate(text[max - 1]) ? max - 1 : max)];
    }

    // The body parsed as T, or null when it is larger than MaxBody.
    public static async Task<T?> ReadAsync<T>(HttpContent content, JsonSerializerOptions options, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            buffer.Write(chunk.AsSpan(0, read));
            if (buffer.Length > MaxBody)
            {
                return default;
            }
        }

        buffer.Position = 0;
        return await JsonSerializer.DeserializeAsync<T>(buffer, options, cancellationToken);
    }
}
