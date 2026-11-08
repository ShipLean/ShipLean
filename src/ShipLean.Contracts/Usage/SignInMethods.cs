namespace ShipLean.Contracts.Usage;

// The values of the method usage property on auth.signed_in, set by the server in each sign-in path.
public static class SignInMethods
{
    public const string Password = "password";
    public const string Google = "google";
    public const string Proxy = "proxy";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) { Password, Google, Proxy };
}
