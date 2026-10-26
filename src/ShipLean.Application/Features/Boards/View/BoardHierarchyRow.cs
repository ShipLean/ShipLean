namespace ShipLean.Application.Features.Boards.View;

// An Epic or Feature of the project (any state), enough to walk up from a card to its Epic and to list the Epic lanes.
public sealed record BoardHierarchyRow(Guid Id, string Key, string Title, string Type, Guid? ParentId, string StateCategory, string BacklogOrder);
