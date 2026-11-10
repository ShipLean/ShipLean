namespace ShipLean.Contracts.Inbox;

// Everything in one project waiting on the signed-in person. OpenAsks counts the asks over Items (the tab badge) and
// OpenItems the rows. EverWaited is false until anything has ever waited on them (the first-run empty state).
// LoadedAtLabel is the server's clock time of the read ("9:12"). Items are in display order: longest waiting first, a
// Story's row under its Feature's. OtherProjects holds the open-ask counts of the person's other projects.
public sealed record InboxDto(
    Guid ProjectId,
    int OpenAsks,
    int OpenItems,
    bool EverWaited,
    string LoadedAtLabel,
    IReadOnlyList<InboxItemDto> Items,
    IReadOnlyList<InboxOtherProjectDto> OtherProjects);
