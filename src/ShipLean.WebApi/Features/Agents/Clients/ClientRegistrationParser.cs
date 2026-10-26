using System.Text.Json;
using ShipLean.Application.Features.Agents.Clients;
using ShipLean.Contracts.Agents;

namespace ShipLean.WebApi.Features.Agents.Clients;

// Reads an RFC 7591 request body into a registration, or says which RFC 7591 error it is.
public static class ClientRegistrationParser
{
    public const int MaxBodyBytes = 16 * 1024;
    private const int MaxNameLength = 200;

    private static readonly string[] KnownGrants = ["authorization_code", "refresh_token"];

    public static (OAuthClientRegistration? Registration, OAuthErrorDto? Error) Parse(ReadOnlySpan<byte> body)
    {
        try
        {
            using var document = JsonDocument.Parse(body.ToArray());
            return ParseObject(document.RootElement);
        }
        catch (JsonException)
        {
            return (null, Metadata("The body must be a JSON object."));
        }
    }

    private static (OAuthClientRegistration?, OAuthErrorDto?) ParseObject(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return (null, Metadata("The body must be a JSON object."));
        }
        if (root.TryGetProperty("token_endpoint_auth_method", out var method)
            && !(method.ValueKind == JsonValueKind.String && method.GetString() == "none"))
        {
            return (null, Metadata("Only token_endpoint_auth_method \"none\" is supported: clients are public."));
        }
        if (root.TryGetProperty("grant_types", out var grants)
            && !(grants.ValueKind == JsonValueKind.Array
                 && grants.EnumerateArray().All(g => g.ValueKind == JsonValueKind.String && KnownGrants.Contains(g.GetString()))))
        {
            return (null, Metadata("grant_types may only hold authorization_code and refresh_token."));
        }
        var name = root.TryGetProperty("client_name", out var nameValue) && nameValue.ValueKind == JsonValueKind.String
            ? nameValue.GetString()?.Trim()
            : null;
        if (string.IsNullOrEmpty(name) || name.Length > MaxNameLength)
        {
            return (null, Metadata($"client_name is required and at most {MaxNameLength} characters."));
        }
        if (!root.TryGetProperty("redirect_uris", out var redirects) || redirects.ValueKind != JsonValueKind.Array
            || redirects.GetArrayLength() is 0 or > RedirectUriRules.MaxRedirects)
        {
            return (null, Redirect($"Give between 1 and {RedirectUriRules.MaxRedirects} redirect_uris."));
        }
        var uris = redirects.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : null).ToArray();
        if (!uris.All(RedirectUriRules.IsAcceptable))
        {
            return (null, Redirect("A redirect URI must be https, http on 127.0.0.1, [::1] or localhost, or an app scheme such as cursor://host/path, with no userinfo or fragment."));
        }
        return (new OAuthClientRegistration(name, uris!), null);
    }

    private static OAuthErrorDto Metadata(string description) => new("invalid_client_metadata", description);

    private static OAuthErrorDto Redirect(string description) => new("invalid_redirect_uri", description);
}
