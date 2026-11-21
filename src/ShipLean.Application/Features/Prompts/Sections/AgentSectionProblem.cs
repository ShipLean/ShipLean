namespace ShipLean.Application.Features.Prompts.Sections;

public sealed record AgentSectionProblem(int Line, AgentSectionProblemKind Kind, string Tag, string? Suggestion);
