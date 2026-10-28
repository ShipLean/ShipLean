using System.Security.Cryptography;
using System.Text;

namespace ShipLean.Application.Features.AgentDispatch;

// Lives with the dispatch code, not WebhookSecrets: a feature may not reference another (ArchitectureTests).
// Linear signs a webhook the way a receiver written for Linear checks it: the `Linear-Signature` header is the lowercase
// hex HMAC-SHA256 of the exact bytes of the body, keyed with the webhook's signing secret.
public static class LinearSignature
{
    public const string Header = "Linear-Signature";

    public static string Sign(string secret, ReadOnlySpan<byte> body)
        => Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body));
}
