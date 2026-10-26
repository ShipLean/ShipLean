using ShipLean.Contracts.Users;

namespace ShipLean.TestSupport.Contracts.Users;

// What a contract test asks its arrange hook for: a user in a status, maybe an instance admin, a member of these projects.
// Name null = a unique one; the email is derived from the name.
public sealed record UserSpec
{
    public UserStatus Status { get; init; } = UserStatus.Active;

    public bool IsInstanceAdmin { get; init; }

    public IReadOnlyCollection<Guid> ProjectIds { get; init; } = [];

    public string? Name { get; init; }

    private static int _counter;

    // The name and email to arrange: Name (or a unique one) and an email derived from it, unique per call.
    public (string Name, string Email) NewIdentity()
    {
        var number = Interlocked.Increment(ref _counter);
        var name = Name ?? $"Arranged Person{number}";
        return (name, $"{name.ToLowerInvariant().Replace(' ', '.')}@example.test");
    }
}
