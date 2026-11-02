using Microsoft.Extensions.Configuration;

namespace ShipLean.Application.Identity;

// The "Sign in with Google" button, from SHIPLEAN_GOOGLE_CLIENT_ID and SHIPLEAN_GOOGLE_CLIENT_SECRET (docs/auth.md). On
// when both are set, off when neither is. Parse throws an InvalidOperationException naming the setting at fault, which
// fails startup (GoogleSignInStartup). A class, not a record, so a stray ToString can't print the secret.
public sealed class GoogleSignInSettings
{
    public const string ClientIdKey = "SHIPLEAN_GOOGLE_CLIENT_ID";
    public const string ClientSecretKey = "SHIPLEAN_GOOGLE_CLIENT_SECRET";

    private GoogleSignInSettings(string? clientId, string? clientSecret)
    {
        ClientId = clientId;
        ClientSecret = clientSecret;
    }

    public string? ClientId { get; }

    public string? ClientSecret { get; }

    public bool IsEnabled => ClientId is not null;

    public static GoogleSignInSettings Parse(IConfiguration configuration, ProxySignInSettings proxy)
    {
        var clientId = Read(configuration, ClientIdKey);
        var clientSecret = Read(configuration, ClientSecretKey);
        if (clientId is null && clientSecret is null)
        {
            return new GoogleSignInSettings(null, null);
        }
        if (clientId is null || clientSecret is null)
        {
            throw new InvalidOperationException($"{ClientIdKey} and {ClientSecretKey} must be set together: {(clientId is null ? ClientIdKey : ClientSecretKey)} is missing.");
        }
        if (proxy.IsProxy)
        {
            throw new InvalidOperationException(
                $"{ClientIdKey} is set while sign-in goes through an identity proxy ({ProxySignInSettings.TokenHeaderKey} or {ProxySignInSettings.EmailHeaderKey}): use one or the other.");
        }
        return new GoogleSignInSettings(clientId, clientSecret);
    }

    private static string? Read(IConfiguration configuration, string key)
        => configuration[key]?.Trim() is { Length: > 0 } value ? value : null;
}
