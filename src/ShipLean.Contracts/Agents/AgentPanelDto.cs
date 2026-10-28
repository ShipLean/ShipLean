namespace ShipLean.Contracts.Agents;

// Everything the Agent panel on a work item page shows, in one response. Visible is false when the item has no session and
// the project has no agent connection (the panel stays out of the way). Sessions are newest first; the first one is the
// current one. Availability says whether Start agent works and, if not, why (Message).
public sealed record AgentPanelDto(bool Visible, AgentStartAvailabilityDto Availability, IReadOnlyList<AgentSessionDto> Sessions);
