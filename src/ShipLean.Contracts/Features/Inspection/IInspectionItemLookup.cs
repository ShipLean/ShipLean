namespace ShipLean.Contracts.Inspection;

// Item key to project and creation date, for an item the caller can read; not_found otherwise. Views build every
// RunQuery and RunRef from it: From is the item's creation, To is now, capped at the maximum search days.
public interface IInspectionItemLookup
{
    Task<InspectionItemInfo> RequireAsync(string projectKey, string itemKey, CancellationToken ct);
}
