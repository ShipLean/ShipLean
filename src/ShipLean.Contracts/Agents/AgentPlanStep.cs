namespace ShipLean.Contracts.Agents;

// Status is pending, inProgress, completed or canceled (Linear's plan statuses).
public sealed record AgentPlanStep(string Content, string Status);
