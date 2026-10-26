namespace ShipLean.WebApi.Features.Mcp.Planning;

// One rule a plan breaks. Field is the argument path it is about, such as stories[2].tasks[0].context_k.
public sealed record PlanFinding(PlanFindingSeverity Severity, string Field, string Message);
