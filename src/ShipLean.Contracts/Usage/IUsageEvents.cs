namespace ShipLean.Contracts.Usage;

public interface IUsageEvents
{
    // Never blocks, never throws.
    void Track(UsageEvent usageEvent);
}
