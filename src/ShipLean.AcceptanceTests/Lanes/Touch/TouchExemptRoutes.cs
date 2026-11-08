namespace ShipLean.AcceptanceTests.Lanes.Touch;

// Routes the phone sweep skips: screen id → reason. Excluded by decision; adding a route here needs Joel's decision,
// not a failing test.
public static class TouchExemptRoutes
{
    public static readonly IReadOnlyDictionary<string, string> All = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["newProject"] = "desktop-only by decision, SHIP-707, 7 Nov 2026",
        ["importStart"] = "desktop-only by decision, SHIP-707, 7 Nov 2026",
        ["jiraImport"] = "desktop-only by decision, SHIP-707, 7 Nov 2026",
        ["authorize"] = "desktop-only by decision, SHIP-707, 7 Nov 2026",
        ["instanceSettings"] = "desktop-only by decision, SHIP-707, 7 Nov 2026",
    };
}
