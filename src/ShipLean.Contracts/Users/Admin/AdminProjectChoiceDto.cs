namespace ShipLean.Contracts.Users.Admin;

// A project the caller administers: an option of the Add user dialog's Projects picker.
public sealed record AdminProjectChoiceDto(Guid Id, string Key, string Name);
