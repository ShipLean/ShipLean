namespace ShipLean.Contracts.Auth;

// GET /api/auth/instance (anonymous): what the sign-in page shows in its top bar, and which sign-in screens the SPA shows.
// SignOutUrl is SHIPLEAN_AUTH_PROXY_SIGN_OUT_URL in proxy mode, and null otherwise or when it is not set. GoogleSignIn says
// whether the sign-in page shows the "Sign in with Google" button.
public sealed record InstanceDto(string Name, SignInMode SignIn, string? SignOutUrl, bool GoogleSignIn);
