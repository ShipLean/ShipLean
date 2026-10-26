namespace ShipLean.Contracts.Auth;

// How people sign in to this instance (InstanceDto.SignIn): ShipLean's own email and password, or an identity proxy in
// front of it (token or plain-header mode alike).
public enum SignInMode
{
    Password,
    Proxy,
}
