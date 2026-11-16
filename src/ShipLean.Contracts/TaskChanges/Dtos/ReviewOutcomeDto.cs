namespace ShipLean.Contracts.TaskChanges.Dtos;

// What Approve and Request changes answer: the item's state name after the move, and the comments still open.
public sealed record ReviewOutcomeDto(string State, int OpenComments);
