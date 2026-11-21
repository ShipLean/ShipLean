namespace ShipLean.Application.Features.Prompts.Sections;

public static class AgentSections
{
    // TODO: replaced by the real agent-section filter
    public static string Filter(string text, string agent) => text;

    // TODO: replaced by the real agent-section check
    public static IReadOnlyList<AgentSectionProblem> Check(string text) => [];
}
