using Microsoft.AspNetCore.Http;
using ShipLean.Application.Identity;
using ShipLean.WebApi.Features.Auth.Proxy;

namespace ShipLean.TestSupport.Hosting.Proxy;

// The proxy identity straight from two test headers, so a test can drive sign-in without minting a token. No email
// header (or one IdentityInput.Email rejects) means no identity. ProxyApiFixture swaps it in unless UseFakeReader is false.
// Kestrel refuses non-ASCII header values, so a name with accents needs a token (TestIdentityProvider) instead.
public sealed class FakeProxyIdentityReader : IProxyIdentityReader
{
    public const string EmailHeader = "X-Test-Proxy-Email";

    public const string NameHeader = "X-Test-Proxy-Name";

    public Task<ProxyIdentity?> ReadAsync(HttpRequest request, CancellationToken ct)
    {
        var email = IdentityInput.Email(Header(request, EmailHeader));
        var identity = email is null ? null : new ProxyIdentity(email, IdentityInput.Name(Header(request, NameHeader)));
        return Task.FromResult(identity);
    }

    private static string? Header(HttpRequest request, string name)
        => request.Headers.TryGetValue(name, out var value) ? value.ToString() : null;
}
