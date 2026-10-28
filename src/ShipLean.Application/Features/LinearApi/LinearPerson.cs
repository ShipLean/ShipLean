namespace ShipLean.Application.Features.LinearApi;

// A person (Linear's User) by id and name; the email is not shared with an agent.
public sealed record LinearPerson(Guid Id, string Name);
