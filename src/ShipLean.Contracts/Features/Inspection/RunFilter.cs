namespace ShipLean.Contracts.Inspection;

// What the run page shows: all turns or one, and every row or only the errors with their ancestors.
public sealed record RunFilter(bool ErrorsOnly, int? Turn)
{
    public static RunFilter All { get; } = new(false, null);
}
