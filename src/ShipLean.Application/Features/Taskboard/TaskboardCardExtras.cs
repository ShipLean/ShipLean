namespace ShipLean.Application.Features.Taskboard;

// The columns a card's extra fields read that the tree rows don't carry.
internal sealed record TaskboardCardExtras(Guid Id, int Priority, int? Severity, DateTimeOffset CreatedAt, string? CreatedByName, DateTimeOffset ChangedAt, string? ChangedByName);
