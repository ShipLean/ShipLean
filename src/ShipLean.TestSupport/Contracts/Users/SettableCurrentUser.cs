using ShipLean.Contracts;

namespace ShipLean.TestSupport.Contracts.Users;

// The ICurrentUser of a contract test: the arrange hook (or the test) sets who the accounts act as.
public sealed class SettableCurrentUser : ICurrentUser
{
    public Guid Id { get; set; }
}
