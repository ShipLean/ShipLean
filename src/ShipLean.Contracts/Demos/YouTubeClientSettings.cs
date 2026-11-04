namespace ShipLean.Contracts.Demos;

public sealed class YouTubeClientSettings(string clientId, string clientSecret)
{
    public string ClientId { get; } = clientId;

    public string ClientSecret { get; } = clientSecret;

    public override string ToString() => nameof(YouTubeClientSettings);
}
