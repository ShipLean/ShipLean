namespace ShipLean.Contracts.Demos;

public sealed record YouTubeTokens(string AccessToken, string RefreshToken, IReadOnlyList<string> GrantedScopes)
{
    public override string ToString() => nameof(YouTubeTokens);
}
