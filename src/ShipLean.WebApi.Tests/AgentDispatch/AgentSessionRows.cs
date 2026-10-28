using ShipLean.Contracts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.AccessSweep;

namespace ShipLean.WebApi.Tests.AgentDispatch;

// The agent session routes on an item: any member may start an agent, ask whether one can start, read the panel's sessions, or stop one.
public sealed class AgentSessionRows : IAccessSweepRows
{
    public IEnumerable<AccessSweepRow> Rows =>
    [
        new("POST", "api/work-items/{id:guid}/agent-session", ProjectRole.Member, Url: $"api/work-items/{SeedIds.WorkItems.Ship90}/agent-session"),
        new("GET", "api/work-items/{id:guid}/agent-session/availability", ProjectRole.Member, Url: $"api/work-items/{SeedIds.WorkItems.Ship90}/agent-session/availability"),
        new("GET", "api/work-items/{id:guid}/agent-sessions", ProjectRole.Member, Url: $"api/work-items/{SeedIds.WorkItems.Ship90}/agent-sessions"),
        new("POST", "api/work-items/{id:guid}/agent-session/stop", ProjectRole.Member, Url: $"api/work-items/{SeedIds.WorkItems.Ship90}/agent-session/stop"),
    ];
}
