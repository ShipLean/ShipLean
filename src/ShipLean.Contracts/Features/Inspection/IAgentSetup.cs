namespace ShipLean.Contracts.Inspection;

// What an agent needs to send telemetry for one item (D24).
public interface IAgentSetup
{
    Task<AgentSetupDto> GetAsync(string projectKey, string agent, string? item, CancellationToken ct);
}
