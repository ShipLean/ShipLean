namespace ShipLean.Application.WorkItems.History;

// Points as the database stores them (numeric): 5.0 and 5 are the same value, so a history diff must not see a change.
public static class WorkItemPoints
{
    public static decimal? Normalise(decimal? points) => points / 1.0000000000000000000000000000m;
}
