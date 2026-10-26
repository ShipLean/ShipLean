using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.WorkItems;

// When a Feature's status line was last written or confirmed, and by whom. Stale is worked out by the server: older than 14 days.
// Agent is the agent stamp ("Claude Code (claude.ai)") and Via the MCP tool name ("set_status"); both are null for a change made in the app.
public sealed record StatusVerifiedDto(DateTimeOffset At, string AtLabel, bool Stale, UserRef? By, string? Agent, string? Via)
{
    public static readonly TimeSpan StaleAfter = TimeSpan.FromDays(14);

    public static bool IsStale(DateTimeOffset at, DateTimeOffset now) => now - at > StaleAfter;
}
