namespace ShipLean.Application.Features.Boards.View;

// The query parameters of GET api/boards/{id}, applied on the server. Q matches a card's key or title (ignoring case).
// Assignee and Owner are repeatable (any of the values): a user id or "none", matching the card's assignee (the Owner on the Features and Epics boards).
// Sprint is a sprint id or "none". Epic is an Epic's id or "none" and matches the card's nearest Epic above it at any depth.
// Older lists "Show older" cursors (BoardCellDto.OlderCursor): the cell holding that cursor returns every card.
public sealed record BoardViewQuery(string? Q = null, IReadOnlyList<string>? Assignee = null, IReadOnlyList<string>? Owner = null, string? Sprint = null, string? Epic = null, IReadOnlyList<string>? Older = null);
