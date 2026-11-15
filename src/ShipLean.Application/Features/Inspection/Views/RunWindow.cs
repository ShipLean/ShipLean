namespace ShipLean.Application.Features.Inspection.Views;

// The time window of every RunQuery and RunRef (K3): from the item's creation to now, no further back than the instance's maximum search days.
public static class RunWindow
{
    public static (DateTimeOffset From, DateTimeOffset To) For(DateTimeOffset itemCreatedAt, DateTimeOffset now, int maxSearchDays)
    {
        var earliest = now.AddDays(-maxSearchDays);
        return (itemCreatedAt > earliest ? itemCreatedAt : earliest, now);
    }
}
