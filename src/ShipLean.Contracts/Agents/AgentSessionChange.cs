namespace ShipLean.Contracts.Agents;

// What agentSessionUpdate changes; null leaves a part alone. ExternalUrls replaces the whole list, then Added are added (a url
// already there takes the new label) and Removed urls are taken out. Plan replaces the whole plan.
public sealed record AgentSessionChange(
    IReadOnlyList<AgentExternalUrl>? ExternalUrls, IReadOnlyList<AgentExternalUrl>? Added, IReadOnlyList<string>? Removed, IReadOnlyList<AgentPlanStep>? Plan);
