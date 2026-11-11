namespace ShipLean.Contracts.Inbox;

// One row of the Inbox: a work item with open asks. Type is the work item type id. UnderKey is the parent Feature's key
// when that Feature's row is in the same list, otherwise null. OldestAt is the earliest of its asks' At; AgeLabel is its
// age ("14h", "4d", "3w"), and LongWait is true past InboxRules.LongWaitAfter. WaitedOnBy holds at most 3 open items this
// one blocks, and WaitedOnByMore counts the rest. Asks are questions, decisions by number, mentions oldest first, then the
// status line.
public sealed record InboxItemDto(
    Guid Id,
    string Key,
    string Type,
    string Title,
    int Revision,
    string? UnderKey,
    DateTimeOffset OldestAt,
    string AgeLabel,
    bool LongWait,
    IReadOnlyList<InboxRefDto> WaitedOnBy,
    int WaitedOnByMore,
    IReadOnlyList<InboxAskDto> Asks);
