namespace ShipLean.Contracts;

// The user the request acts as. It is the single source of the user id for the API: nothing takes a user id as a
// parameter.
public interface ICurrentUser
{
    Guid Id { get; }
}
