using ShipLean.Application.Features.Mcp.Agents;

namespace ShipLean.WebApi.Features.Mcp.Tools.Agents;

// What get_agent_session reads. The item was resolved and the caller's access checked before this is called, so an
// implementation never decides who may see the session, only reads it.
public interface IAgentSessionDetails
{
    // session: that session (it belongs to the item); null means the item's newest. before is the cursor from a previous page.
    Task<AgentSessionDetail> ReadAsync(Guid itemId, string itemKey, Guid? session, string? before, int limit, CancellationToken cancellationToken);
}

// A stand-in that returns the final shape with fixed fake values.
// TODO: read the session, its activities (capped at 4 KB each, newest first, paged by before), its delivery attempts and its run log attachment for real.
public sealed class StubAgentSessionDetails : IAgentSessionDetails
{
    public static readonly Guid FakeSessionId = new("0000000e-0000-4000-8001-0000000000a1");

    public Task<AgentSessionDetail> ReadAsync(Guid itemId, string itemKey, Guid? session, string? before, int limit, CancellationToken cancellationToken)
    {
        var started = new DateTimeOffset(2026, 11, 15, 9, 0, 0, TimeSpan.Zero);
        var row = new AgentSessionRow(session ?? FakeSessionId, "Home relay", "failed", started, started.AddSeconds(10), "deliveryFailed", "No answer within 5 seconds. (2 tries)");
        var detail = new AgentSessionDetail(
            row,
            itemKey,
            new(Guid.Parse("0000000d-0000-4000-8001-0000000000a1"), $"agent-run-{row.Id}.log", 18432, "/api/attachments/0000000d-0000-4000-8001-0000000000a1"),
            [new(Guid.Parse("0000000f-0000-4000-8001-0000000000a1"), "error", "Fake activity body.", null, null, null, false, started.AddSeconds(5))],
            null,
            [new("created", 1, started, "fake", null, "No answer within 5 seconds.", 5000, started.AddSeconds(5), "fake")]);
        return Task.FromResult(detail);
    }
}
