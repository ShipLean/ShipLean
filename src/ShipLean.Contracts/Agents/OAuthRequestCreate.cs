namespace ShipLean.Contracts.Agents;

// A pending /oauth/authorize request: the columns of oauth_request. The client and redirect are already proven.
public sealed record OAuthRequestCreate(
    string ClientId,
    string RedirectUri,
    string? State,
    string CodeChallenge,
    IReadOnlyList<string> Scopes,
    string Resource,
    Guid UserId)
{
    // How long the person has to decide on the consent page.
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
}
