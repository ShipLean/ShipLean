namespace ShipLean.Contracts.Inbox;

// Another project of the signed-in person, with its open-ask count.
public sealed record InboxOtherProjectDto(Guid ProjectId, string ProjectKey, int OpenAsks);
