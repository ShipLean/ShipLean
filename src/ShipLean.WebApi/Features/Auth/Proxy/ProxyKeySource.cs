using Agoda.IoC.Core;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using ShipLean.Application.Identity;

namespace ShipLean.WebApi.Features.Auth.Proxy;

// Token mode's signing keys: one ConfigurationManager for the process, so keys are cached, refreshed in the background,
// and refetched (under the library's rate limit) when a token names an unknown kid. The keys come from the JWKS URL
// when it is set, or else from the issuer's discovery document (<issuer>/.well-known/openid-configuration), whose own
// issuer must be the configured one and whose jwks_uri must pass IsDiscoveredJwksUrl. Every fetch goes through the
// proxy-sign-in-keys client, which follows no redirects, so each address fetched is one that was checked.
[RegisterSingleton]
public sealed class ProxyKeySource
{
    public ProxyKeySource(ProxySignInSettings settings, IHttpClientFactory httpClients)
    {
        if (settings.Mode != ProxySignInMode.Token)
        {
            return;
        }
        var documents = new KeyDocumentRetriever(httpClients);
        Manager = settings.JwksUrl is { } jwksUrl
            ? new ConfigurationManager<OpenIdConnectConfiguration>(jwksUrl, new JwksRetriever(), documents)
            : new ConfigurationManager<OpenIdConnectConfiguration>(
                $"{settings.Issuer!.TrimEnd('/')}/.well-known/openid-configuration", new DiscoveryRetriever(settings.Issuer), documents);
    }

    // Null outside token mode.
    public ConfigurationManager<OpenIdConnectConfiguration>? Manager { get; }

    // A jwks_uri a discovery document may name: a key URL (https, or loopback http, no credentials), and loopback only
    // when the issuer itself is, so a remote document can't make this host probe its own local ports.
    public static bool IsDiscoveredJwksUrl(string issuer, string jwksUri)
        => ProxySignInSettings.IsKeyUrl(jwksUri)
           && (!new Uri(jwksUri).IsLoopback || (Uri.TryCreate(issuer, UriKind.Absolute, out var issuerUri) && issuerUri.IsLoopback));

    private static void AddKeys(OpenIdConnectConfiguration configuration, string jwks)
    {
        var keys = new JsonWebKeySet(jwks);
        configuration.JsonWebKeySet = keys;
        foreach (var key in keys.GetSigningKeys())
        {
            configuration.SigningKeys.Add(key);
        }
    }

    // Fetches a key document only from a key URL (ProxySignInSettings.IsKeyUrl). Failures are IOExceptions naming the
    // host and path and the kind of failure (status, unreachable, timeout), never the response body. A fresh client per
    // fetch keeps the factory's handler rotation.
    private sealed class KeyDocumentRetriever(IHttpClientFactory httpClients) : IDocumentRetriever
    {
        public async Task<string> GetDocumentAsync(string address, CancellationToken cancel)
        {
            if (!ProxySignInSettings.IsKeyUrl(address))
            {
                throw new InvalidOperationException(
                    "A signing-key address is neither https nor loopback http without credentials, so it was not fetched.");
            }
            var uri = new Uri(address);
            using var client = httpClients.CreateClient(ProxyIdentityServices.HttpClientName);
            try
            {
                using var response = await client.GetAsync(uri, cancel);
                if (!response.IsSuccessStatusCode)
                {
                    throw new IOException($"The signing-key endpoint {Where(uri)} answered {(int)response.StatusCode}.");
                }
                return await response.Content.ReadAsStringAsync(cancel);
            }
            catch (HttpRequestException e)
            {
                throw new IOException($"The signing-key endpoint {Where(uri)} could not be reached ({e.HttpRequestError}).");
            }
            catch (TaskCanceledException) when (!cancel.IsCancellationRequested)
            {
                throw new IOException($"The signing-key endpoint {Where(uri)} did not answer within {client.Timeout.TotalSeconds} s.");
            }
        }

        // Host, port and path only: never a query string, which could carry a secret.
        private static string Where(Uri uri) => uri.IsDefaultPort ? $"{uri.Host}{uri.AbsolutePath}" : $"{uri.Host}:{uri.Port}{uri.AbsolutePath}";
    }

    // SHIPLEAN_AUTH_PROXY_TOKEN_JWKS_URL: a configuration holding only the JWKS's signing keys (no issuer; the token
    // handler checks the issuer against the configured one).
    private sealed class JwksRetriever : IConfigurationRetriever<OpenIdConnectConfiguration>
    {
        public async Task<OpenIdConnectConfiguration> GetConfigurationAsync(string address, IDocumentRetriever retriever, CancellationToken cancel)
        {
            var configuration = new OpenIdConnectConfiguration { JwksUri = address };
            AddKeys(configuration, await retriever.GetDocumentAsync(address, cancel));
            return configuration;
        }
    }

    // Discovery. OpenID Connect Discovery requires the document's issuer to be identical to the issuer it was fetched
    // for; checking it keeps the token handler, which also accepts the configuration's issuer, to the configured one.
    // The jwks_uri is checked before it is fetched.
    private sealed class DiscoveryRetriever(string issuer) : IConfigurationRetriever<OpenIdConnectConfiguration>
    {
        public async Task<OpenIdConnectConfiguration> GetConfigurationAsync(string address, IDocumentRetriever retriever, CancellationToken cancel)
        {
            var configuration = OpenIdConnectConfiguration.Create(await retriever.GetDocumentAsync(address, cancel));
            if (!string.Equals(configuration.Issuer, issuer, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"The discovery document names an issuer other than {ProxySignInSettings.IssuerKey}, so its keys are not trusted.");
            }
            if (string.IsNullOrEmpty(configuration.JwksUri) || !IsDiscoveredJwksUrl(issuer, configuration.JwksUri))
            {
                throw new InvalidOperationException(
                    "The discovery document's jwks_uri is missing, not https, or loopback for a remote issuer, so it was not fetched.");
            }
            AddKeys(configuration, await retriever.GetDocumentAsync(configuration.JwksUri, cancel));
            return configuration;
        }
    }
}
