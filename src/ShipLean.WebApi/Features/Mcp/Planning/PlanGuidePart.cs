namespace ShipLean.WebApi.Features.Mcp.Planning;

// The parts of the planning guide. Each is one embedded Markdown file, named by its wire name.
public enum PlanGuidePart
{
    Work,
    Guide,
    Brief,
    Report,
    PlanReviewer,
    CodeReviewer,
    Fix,
    Lead,
    Retro,
}
