using Microsoft.Extensions.Configuration;
using ShipLean.Contracts.Demos;
using ShipLean.Application.Features.Instance.Integrations;

namespace ShipLean.Application.Features.Demos.YouTube;

// The YouTube OAuth client from SHIPLEAN_YOUTUBE_CLIENT_ID and SHIPLEAN_YOUTUBE_CLIENT_SECRET. It is its own client, never
// the sign-in one (SHIPLEAN_GOOGLE_*). Both or neither: one without the other throws, naming the one that is missing.
public static class YouTubeClientEnv
{
    public const string ClientIdKey = "SHIPLEAN_YOUTUBE_CLIENT_ID";
    public const string ClientSecretKey = "SHIPLEAN_YOUTUBE_CLIENT_SECRET";

    // Called with the configuration of the moment, never a copy taken while services were being added.
    public static YouTubeClientSettings? Read(IConfiguration configuration)
    {
        var clientId = IntegrationEnv.Read(configuration, ClientIdKey);
        var clientSecret = IntegrationEnv.Read(configuration, ClientSecretKey);
        if (clientId is null && clientSecret is null)
        {
            return null;
        }
        if (clientId is null || clientSecret is null)
        {
            throw new InvalidOperationException($"{ClientIdKey} and {ClientSecretKey} must be set together: {(clientId is null ? ClientIdKey : ClientSecretKey)} is missing.");
        }
        return new YouTubeClientSettings(clientId, clientSecret);
    }
}
