namespace ShipLean.TestSupport.Hosting;

// Who a test host's requests act as when they carry no sign-in cookie (TestAuthentication). Joel is the default for
// every API fixture, so existing tests run signed in as the seeded instance admin; Anonymous sends requests with no
// user at all. Email and Password are the seed's, for the real sign-in an acceptance test does.
public sealed record TestUser(Guid? Id, string? Email = null, string? Password = null)
{
    public static TestUser Joel { get; } = new(SeedIds.Users.Joel, "joel@shiplean.local", SeedIds.Passwords.Joel);

    public static TestUser Priya { get; } = new(SeedIds.Users.Priya, "priya@shiplean.local", SeedIds.Passwords.Priya);

    public static TestUser Tom { get; } = new(SeedIds.Users.Tom, "tom@shiplean.local", SeedIds.Passwords.Tom);

    public static TestUser Mei { get; } = new(SeedIds.Users.Mei, "mei@shiplean.local", SeedIds.Passwords.Mei);

    public static TestUser Sam { get; } = new(SeedIds.Users.Sam, "sam@shiplean.local", SeedIds.Passwords.Sam);

    public static TestUser Kai { get; } = new(SeedIds.Users.Kai, "kai@shiplean.local", SeedIds.Passwords.Kai);

    public static TestUser Anonymous { get; } = new((Guid?)null);

    // A user arranged by the test: API requests can act as them; the acceptance sign-in needs the email and password too.
    public static TestUser Of(Guid id, string? email = null, string? password = null) => new(id, email, password);
}
