using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.Webhooks;

// Each provider's endpoint exists only while its secret is set, and the same value goes into the provider's webhook
// settings. Read at call time, so a test host can set it. Both checks compare in constant time.
public static class WebhookSecrets
{
    public const string GithubKey = "SHIPLEAN_GITHUB_WEBHOOK_SECRET";
    public const string GitlabKey = "SHIPLEAN_GITLAB_WEBHOOK_SECRET";
    public const string GithubSignatureHeader = "X-Hub-Signature-256";
    public const string GitlabTokenHeader = "X-Gitlab-Token";

    private const string SignaturePrefix = "sha256=";

    // Null when the endpoint is turned off.
    public static string? For(WebhookProvider provider, IConfiguration configuration)
    {
        var secret = configuration[provider == WebhookProvider.Github ? GithubKey : GitlabKey];
        return string.IsNullOrWhiteSpace(secret) ? null : secret;
    }

    // GitHub: X-Hub-Signature-256 is "sha256=" and the hex HMAC-SHA256 of the raw body, keyed by the secret.
    public static bool GithubSignatureMatches(string secret, string? header, ReadOnlySpan<byte> body)
    {
        if (header is null || !header.StartsWith(SignaturePrefix, StringComparison.Ordinal))
        {
            return false;
        }
        byte[] given;
        try
        {
            given = Convert.FromHexString(header.AsSpan(SignaturePrefix.Length));
        }
        catch (FormatException)
        {
            return false;
        }
        return CryptographicOperations.FixedTimeEquals(given, HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body));
    }

    // GitLab: X-Gitlab-Token is the secret itself. Both sides are hashed first, so the compare doesn't leak the length.
    public static bool GitlabTokenMatches(string secret, string? header)
        => header is not null && CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(header)), SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
}
