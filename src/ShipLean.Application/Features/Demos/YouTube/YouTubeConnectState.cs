using System.Globalization;
using System.Security.Cryptography;
using Agoda.IoC.Core;
using Microsoft.AspNetCore.DataProtection;

namespace ShipLean.Application.Features.Demos.YouTube;

// The `state` of a YouTube consent: Data Protection (a purpose of its own, so neither the sign-in cookie's protector nor the
// integration tokens' can open one) over who started it, for which project, and until when. Only authentic states open; the caller
// decides whether one has expired.
[RegisterSingleton]
public sealed class YouTubeConnectState(IDataProtectionProvider provider)
{
    public const string Purpose = "youtube-connect";
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
    private const int MaxLength = 1024;

    private readonly IDataProtector _protector = provider.CreateProtector(Purpose);

    public sealed record Grant(Guid UserId, Guid ProjectId, DateTimeOffset ExpiresAt);

    public string Seal(Guid userId, Guid projectId, DateTimeOffset expiresAt)
        => _protector.Protect(string.Create(CultureInfo.InvariantCulture, $"{userId:N}|{projectId:N}|{expiresAt.ToUnixTimeSeconds()}"));

    // Null for anything that is not a state this instance sealed under this purpose, untouched.
    public Grant? Open(string? state)
    {
        if (string.IsNullOrEmpty(state) || state.Length > MaxLength)
        {
            return null;
        }

        string plain;
        try
        {
            plain = _protector.Unprotect(state);
        }
        catch (CryptographicException)
        {
            return null;
        }

        var parts = plain.Split('|');
        return parts.Length == 3
            && Guid.TryParseExact(parts[0], "N", out var user)
            && Guid.TryParseExact(parts[1], "N", out var project)
            && long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var expires)
            ? new Grant(user, project, DateTimeOffset.FromUnixTimeSeconds(expires))
            : null;
    }
}
