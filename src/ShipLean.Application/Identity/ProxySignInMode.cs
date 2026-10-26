namespace ShipLean.Application.Identity;

// Which sign-in ProxySignInSettings switched on: ShipLean's own password, a signed token from the proxy, or the proxy's
// plain email header.
public enum ProxySignInMode
{
    Password,
    Token,
    Header,
}
