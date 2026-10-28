namespace ShipLean.Contracts.Agents;

// One step of the agent's plan. Status is pending, inProgress, completed or canceled (Linear's plan statuses).
public sealed record AgentPlanStepDto(string Content, string Status);
