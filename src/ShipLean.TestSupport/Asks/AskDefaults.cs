namespace ShipLean.TestSupport.Asks;

// What the ask builders default to: raised by Joel through this agent, now by the real clock (whole microseconds, so a value
// read back from Postgres equals the one written).
public static class AskDefaults
{
    public const string AgentName = "Claude Code (test)";

    public static DateTimeOffset Now()
    {
        var now = TimeProvider.System.GetUtcNow();
        return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMicrosecond));
    }
}
