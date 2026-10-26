using System.Security.Claims;

namespace ShipLean.WebApi.Features.Auth;

// The cookie's principal: the user id (NameIdentifier, which CurrentUser reads) and the security stamp it was issued
// under (CookieSessionEvents ends the session once app_user's stamp moves on). A cookie issued by proxy sign-in also
// carries the identity it was issued for, so a request whose proxy identity still matches needs no database read.
public static class AuthPrincipal
{
    public const string StampClaim = "shiplean:stamp";
    public const string ProxyEmailClaim = "shiplean:proxy-email";
    public const string ProxyNameClaim = "shiplean:proxy-name";

    public static ClaimsPrincipal For(Guid userId, string stamp) => Principal(SessionClaims(userId, stamp));

    // The proxy name claim is left out when the provider sent no name.
    public static ClaimsPrincipal ForProxy(Guid userId, string stamp, string email, string? name)
    {
        var claims = SessionClaims(userId, stamp);
        claims.Add(new Claim(ProxyEmailClaim, email));
        if (name != null)
        {
            claims.Add(new Claim(ProxyNameClaim, name));
        }
        return Principal(claims);
    }

    private static List<Claim> SessionClaims(Guid userId, string stamp) =>
    [
        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
        new Claim(StampClaim, stamp),
    ];

    private static ClaimsPrincipal Principal(IEnumerable<Claim> claims) => new(new ClaimsIdentity(claims, AuthServices.CookieScheme));
}
