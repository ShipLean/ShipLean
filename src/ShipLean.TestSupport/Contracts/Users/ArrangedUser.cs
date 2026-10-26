namespace ShipLean.TestSupport.Contracts.Users;

// An arranged user. The password is AppUserBuilder.ArrangedPassword unless the status has none.
public sealed record ArrangedUser(Guid Id, string Email, string Name);
