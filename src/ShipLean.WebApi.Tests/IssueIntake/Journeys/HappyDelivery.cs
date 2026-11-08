using System.Security.Cryptography;
using System.Text;
using ShipLean.TestSupport.Webhooks;

namespace ShipLean.WebApi.Tests.IssueIntake.Journeys;

// The recorded payloads all name acme/storefront. A journey that owns a repository of its own rewrites those fields and signs the body again.
internal static class HappyDelivery
{
    public static WebhookDelivery Retarget(WebhookDelivery delivery, string repository, string secret)
    {
        var name = repository[(repository.IndexOf('/', StringComparison.Ordinal) + 1)..];
        var body = delivery.Body
            .Replace("Acme/Storefront", repository, StringComparison.Ordinal)
            .Replace("acme/storefront", repository, StringComparison.Ordinal)
            .Replace("\"name\":\"storefront\"", $"\"name\":\"{name}\"", StringComparison.Ordinal);
        var signature = "sha256=" + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body)));
        return (delivery with { Body = body }).WithHeader("X-Hub-Signature-256", signature);
    }
}
