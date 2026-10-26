using ShipLean.Application.Features.Agents.Clients;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Agents.Stores;

// What dynamic client registration accepts (RFC 7591, with the redirect rules of OAuth 2.1 for public clients).
internal static class ClientRegistrationRules
{
    private const int MaxNameLength = 200;

    // The trimmed name and the redirect URIs, or Validation.
    public static (string Name, string[] RedirectUris) Check(OAuthClientRegistration request)
    {
        var name = request.ClientName?.Trim() ?? string.Empty;
        if (name.Length == 0 || name.Length > MaxNameLength)
        {
            throw ApiException.Validation($"client_name is required and at most {MaxNameLength} characters.");
        }
        if (request.RedirectUris is not { Count: > 0 and <= RedirectUriRules.MaxRedirects })
        {
            throw ApiException.Validation($"Give between 1 and {RedirectUriRules.MaxRedirects} redirect URIs.");
        }
        if (!request.RedirectUris.All(RedirectUriRules.IsAcceptable))
        {
            throw ApiException.Validation("A redirect URI must be https, http on 127.0.0.1, [::1] or localhost, or an app scheme such as cursor://host/path, and carry no userinfo or fragment.");
        }
        return (name, request.RedirectUris.ToArray());
    }
}
