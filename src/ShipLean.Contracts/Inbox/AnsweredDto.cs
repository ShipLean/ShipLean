namespace ShipLean.Contracts.Inbox;

// What the signed-in person answered in one project in the last InboxRules.AnsweredWindow, newest answer first, at most
// InboxRules.AnsweredLimit rows. AnsweredToday counts today's answers and PickedUpToday those an agent has since picked up.
public sealed record AnsweredDto(int AnsweredToday, int PickedUpToday, IReadOnlyList<AnsweredRowDto> Rows);
