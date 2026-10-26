using System.Security.Cryptography;
using Agoda.IoC.Core;
using Microsoft.AspNetCore.DataProtection;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Instance.Integrations;

// GitHub and GitLab tokens at rest: ASP.NET Data Protection (keys are in Postgres, data_protection_key) under a purpose of
// their own, so neither the sign-in cookie's protector nor the Jira one can open one. The plaintext exists only in memory.
[RegisterSingleton]
public sealed class IntegrationTokens(IDataProtectionProvider provider)
{
    public const string Purpose = "ShipLean.Instance.Integrations.Token.v1";
    private const int LastCharacters = 4;
    private const int MaxLength = 512;

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
            throw ApiException.NotConfigured("A saved integration token can no longer be read. Replace or clear it in Instance settings.");
        }
    }

    // The only part of a token that ever leaves the server. A token too short to hide behind its last four shows nothing.
    public static string LastFour(string token) => token.Length < 2 * LastCharacters ? string.Empty : token[^LastCharacters..];

    // A token as typed: trimmed, one line of printable characters. The message never repeats the value.
    public static string Require(string? value, string field)
    {
        var token = (value ?? string.Empty).Trim();
        if (token.Length == 0 || token.Length > MaxLength || token.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
        {
            throw ApiException.Validation($"The token must be one line of at most {MaxLength} characters with no spaces.", field);
        }
        return token;
    }
}
