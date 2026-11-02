namespace ShipLean.Contracts.WorkItems;

// Where a placed row lands: at the top of the backlog, at the bottom, or just above another row.
public enum BatchSlot
{
    First,
    Last,
    Before,
}
