namespace ShipLean.Contracts.Inbox;

// One answer, or a batch of answers on one item. Type is the work item type id. Labels are the asks it answered ("D1–D8",
// "Q1", "@"), and Summary says what was done ("Approved D1–D8", "Answered Q1", "Replied to Priya Shah", "Marked seen").
// Next is the first thing after the answer on that item, or null when nothing has happened yet.
public sealed record AnsweredRowDto(
    Guid ItemId,
    string Key,
    string Type,
    string Title,
    InboxAskKind Kind,
    string Labels,
    string Summary,
    DateTimeOffset AnsweredAt,
    string AnsweredAtLabel,
    AnsweredNextDto? Next);
