using System.Text.Json;
using ShipLean.Contracts.Agents;

namespace ShipLean.Application.Features.Agents.Clients;

// Turns the body of a metadata document into the client it describes, or null when it must be refused: not a JSON object,
// client_id not the URL, any secret or non-"none" token_endpoint_auth_method (client_secret_*, private_key_jwt), or no
// valid redirect_uris.
public static class ClientMetadataParser
{
    private const int MaxNameLength = 200;

    public static OAuthClient? Parse(string clientId, Uri url, ReadOnlySpan<byte> body)
    {
        try
        {
            using var document = JsonDocument.Parse(body.ToArray());
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || Text(root, "client_id") != clientId
                || root.TryGetProperty("client_secret", out _)
                || root.TryGetProperty("jwks", out _)
                || root.TryGetProperty("jwks_uri", out _))
            {
                return null;
            }
            if (root.TryGetProperty("token_endpoint_auth_method", out var method)
                && !(method.ValueKind == JsonValueKind.String && method.GetString() == "none"))
            {
                return null;
            }
            if (!root.TryGetProperty("redirect_uris", out var redirects) || redirects.ValueKind != JsonValueKind.Array
                || redirects.GetArrayLength() is 0 or > RedirectUriRules.MaxRedirects)
            {
                return null;
            }
            var uris = redirects.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : null).ToArray();
            if (!uris.All(RedirectUriRules.IsAcceptable))
            {
                return null;
            }
            var name = Text(root, "client_name")?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                name = url.Host;
            }
            else if (name.Length > MaxNameLength)
            {
                name = name[..MaxNameLength];
            }
            return new OAuthClient(clientId, OAuthClientKind.MetadataDocument, name, url.Host, uris!);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Text(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
