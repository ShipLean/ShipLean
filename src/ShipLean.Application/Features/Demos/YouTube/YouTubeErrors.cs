using System.Text.Json;
using ShipLean.Contracts.Demos;

namespace ShipLean.Application.Features.Demos.YouTube;

// Turns a Google error answer into a YouTubeFailureException. The message is fixed text per kind: Google's own message and
// description are never copied, because an OAuth error description can quote the token.
internal static class YouTubeErrors
{
    private const int MaxBody = 64 * 1024;

    // Reasons the Data API gives for a spent quota or upload limit (videos.insert lists uploadLimitExceeded).
    private static readonly HashSet<string> QuotaReasons = new(StringComparer.Ordinal)
    {
        "quotaExceeded", "dailyLimitExceeded", "rateLimitExceeded", "userRateLimitExceeded", "uploadLimitExceeded",
    };

    public static async Task<YouTubeFailureException> FromAsync(HttpResponseMessage response, string what, CancellationToken cancellationToken)
    {
        var (oauthError, reasons) = await ReadAsync(response, cancellationToken);
        var status = (int)response.StatusCode;
        if (oauthError == "invalid_grant" || status == 401)
        {
            return Failure(YouTubeFailureKind.Revoked, $"YouTube no longer accepts the saved access while {what}. Connect the channel again.");
        }

        if (reasons.Any(QuotaReasons.Contains))
        {
            return Failure(YouTubeFailureKind.Quota, $"YouTube's quota or upload limit is used up for now ({what}). Try again later.");
        }

        return status >= 500
            ? Failure(YouTubeFailureKind.Network, $"YouTube had a problem while {what} (status {status}). Try again later.")
            : Failure(YouTubeFailureKind.Rejected, $"YouTube refused the request while {what} (status {status}).");
    }

    public static YouTubeFailureException Failure(YouTubeFailureKind kind, string message) => new(kind, message);

    public static YouTubeFailureException Unreachable(string what) =>
        Failure(YouTubeFailureKind.Network, $"ShipLean couldn't reach YouTube while {what}. Try again later.");

    private static async Task<(string? OAuthError, string[] Reasons)> ReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var limited = new MemoryStream();
            var buffer = new byte[4096];
            int read;
            while (limited.Length < MaxBody && (read = await body.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await limited.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }

            limited.Position = 0;
            using var json = await JsonDocument.ParseAsync(limited, cancellationToken: cancellationToken);
            return Parse(json.RootElement);
        }
        catch (JsonException)
        {
            return (null, []);
        }
        catch (HttpRequestException)
        {
            return (null, []);
        }
    }

    private static (string? OAuthError, string[] Reasons) Parse(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("error", out var error))
        {
            return (null, []);
        }

        // OAuth endpoints: {"error":"invalid_grant"}. Data API: {"error":{"errors":[{"reason":"quotaExceeded"}]}}.
        if (error.ValueKind == JsonValueKind.String)
        {
            return (error.GetString(), []);
        }

        var reasons = error.ValueKind == JsonValueKind.Object && error.TryGetProperty("errors", out var list) && list.ValueKind == JsonValueKind.Array
            ? list.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.Object && item.TryGetProperty("reason", out var reason) ? reason.GetString() : null).OfType<string>().ToArray()
            : [];
        return (null, reasons);
    }
}
