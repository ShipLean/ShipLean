using System.Security.Cryptography;
using Agoda.IoC.Core;
using Microsoft.AspNetCore.DataProtection;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Backups.Config;

// The backup destination's secret key and the backup passphrase at rest (decision D1): ASP.NET Data Protection (keys
// are in Postgres) under a purpose of its own, so no other protector can open them. Only the last four characters of
// the secret key ever leave the server; the passphrase never does.
[RegisterSingleton]
public sealed class BackupSecrets(IDataProtectionProvider provider)
{
    public const string Purpose = "ShipLean.Instance.Backups.Secret.v1";
    private const int LastCharacters = 4;

    private readonly IDataProtector _protector = provider.CreateProtector(Purpose);

    // A secret too short to hide behind its last four shows nothing.
    public static string LastFour(string secret) => secret.Length < 2 * LastCharacters ? string.Empty : secret[^LastCharacters..];

    public string Protect(string secret) => _protector.Protect(secret);

    // The plaintext, or null when the key ring can no longer open it (keys lost or rotated away). A save that replaces
    // the secret never needs the old one.
    public string? TryUnprotect(string protectedSecret)
    {
        try
        {
            return _protector.Unprotect(protectedSecret);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    public string Unprotect(string protectedSecret)
    {
        try
        {
            return _protector.Unprotect(protectedSecret);
        }
        catch (CryptographicException)
        {
            throw ApiException.NotConfigured("A saved backup secret can no longer be read. Enter the secret key and passphrase again in Instance settings › Backups.");
        }
    }
}
