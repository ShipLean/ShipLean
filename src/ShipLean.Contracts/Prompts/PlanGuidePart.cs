namespace ShipLean.Contracts.Prompts;

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
