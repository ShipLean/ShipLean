namespace ShipLean.Contracts.Inspection;

// Whether an item page shows Agent runs: Inspection is on for the instance and for the item's project.
public interface IInspectionFlag
{
    Task<bool> IsOnAsync(Guid projectId, CancellationToken ct);
}
