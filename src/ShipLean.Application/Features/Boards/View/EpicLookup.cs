namespace ShipLean.Application.Features.Boards.View;

// Walks up from a card to the nearest Epic above it, through any number of nested Features (Removed ones included).
public sealed class EpicLookup(IReadOnlyList<BoardHierarchyRow> hierarchy)
{
    private readonly Dictionary<Guid, BoardHierarchyRow> _byId = hierarchy.ToDictionary(r => r.Id);

    public IReadOnlyList<BoardHierarchyRow> Rows => hierarchy;

    // Null when the chain ends at a Feature with no parent (or the card has none).
    public Guid? EpicOf(Guid? parentId)
    {
        var seen = new HashSet<Guid>();
        var current = parentId;
        while (current is { } id && seen.Add(id) && _byId.TryGetValue(id, out var row))
        {
            if (row.Type == "epic")
            {
                return row.Id;
            }

            current = row.ParentId;
        }

        return null;
    }
}
