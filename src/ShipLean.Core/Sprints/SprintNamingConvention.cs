namespace ShipLean.Core.Sprints;

// How a project names the sprints it makes by default. Changing it renames nothing; it applies to the next default name.
public enum SprintNamingConvention
{
    // "2026 Q4 Sprint 3": the calendar quarter of the start date, and the sprint's place in it.
    YearQuarterNumber,

    // "Sprint 129": one more than the highest number any sprint's name ends in.
    Number,
}
