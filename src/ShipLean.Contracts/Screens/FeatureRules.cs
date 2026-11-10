namespace ShipLean.Contracts.Screens;

// The Features screens' fixed rules. StatusStaleAfter is the phone's "status is stale" age; the web's Verified chip keeps its own
// (StatusVerifiedDto.StaleAfter).
public static class FeatureRules
{
    public static readonly TimeSpan StatusStaleAfter = TimeSpan.FromDays(3);
}
