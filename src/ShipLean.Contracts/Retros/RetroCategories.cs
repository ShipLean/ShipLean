namespace ShipLean.Contracts.Retros;

// The one display order of the four categories, on every screen. Only the action dialog differs (it offers Can be improved, Went badly, Not in the plan).
public static class RetroCategories
{
    public static readonly IReadOnlyList<RetroCategory> Display = [RetroCategory.WentWell, RetroCategory.NotInPlan, RetroCategory.WentBadly, RetroCategory.Improve];
}
