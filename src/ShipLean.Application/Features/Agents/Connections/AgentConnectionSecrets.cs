using System.Security.Cryptography;
using Agoda.IoC.Core;
using Microsoft.AspNetCore.DataProtection;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Agents.Connections;

// The signing secret at rest: ASP.NET Data Protection (keys are in Postgres) under a purpose of its own, so no other
// protector (sign-in cookie, Jira, integrations) can open one. The callback token is never decrypted: only its hash is stored.
[RegisterSingleton]
public sealed class AgentConnectionSecrets(IDataProtectionProvider provider)
{
    public const string Purpose = "ShipLean.Agents.Connections.SigningSecret.v1";
    private const int LastCharacters = 4;

    private readonly IDataProtector _protector = provider.CreateProtector(Purpose);

    // 32 random bytes, base64url: a signing secret or a callback token as it is first shown.
    public static string New() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string LastFour(string secret) => secret[^LastCharacters..];

    public string Protect(string secret) => _protector.Protect(secret);

    public string Unprotect(string protectedSecret)
    {
        try
        {
            return _protector.Unprotect(protectedSecret);
        }
        catch (CryptographicException)
        {
            throw ApiException.NotConfigured("A saved agent connection secret can no longer be read. Regenerate it in Instance settings.");
        }
    }
}
