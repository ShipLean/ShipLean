namespace ShipLean.Contracts.Agents;

// Every agent sign-in slice takes issuer, iss and resource from here. Issuer is instance_settings.base_url, else the
// request's own scheme, host and port; Resource is the issuer plus /mcp. Read on every call, never cached.
public interface IAgentAddresses
{
    Task<AgentAddresses> ForCurrentRequestAsync(CancellationToken cancellationToken = default);
}
