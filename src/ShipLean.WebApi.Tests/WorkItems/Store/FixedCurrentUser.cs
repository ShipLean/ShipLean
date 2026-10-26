using ShipLean.Contracts;

namespace ShipLean.WebApi.Tests.WorkItems.Store;

// The user a store acts as when no request is running.
public sealed class FixedCurrentUser(Guid id) : ICurrentUser
{
    public Guid Id { get; } = id;
}
