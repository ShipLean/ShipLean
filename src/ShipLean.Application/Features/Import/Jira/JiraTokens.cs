using System.Security.Cryptography;
using Agoda.IoC.Core;
using Microsoft.AspNetCore.DataProtection;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Import.Jira;

// Jira tokens at rest: ASP.NET Data Protection (keys are in Postgres, data_protection_key) under a purpose of their own, so
// the sign-in cookie's protector cannot open one. The plaintext exists only in memory.
[RegisterSingleton]
public sealed class JiraTokens(IDataProtectionProvider provider)
{
    public const string Purpose = "ShipLean.Import.Jira.Token.v1";
    private const int LastCharacters = 4;

    private readonly IDataProtector _protector = provider.CreateProtector(Purpose);

    public string Protect(string token) => _protector.Protect(token);

    public string Unprotect(string protectedToken)
    {
        try
        {
            return _protector.Unprotect(protectedToken);
        }
        catch (CryptographicException)
        {
            throw ApiException.NotConfigured("The saved Jira token can no longer be read. Save the Jira connection again.");
        }
    }

    // The only part of a token that ever leaves the server. A token too short to hide behind its last four shows nothing.
    public static string LastFour(string token) => token.Length < 2 * LastCharacters ? string.Empty : token[^LastCharacters..];
}
