namespace ShipLean.AcceptanceTests.Lanes.Touch;

// Routes not yet mobile: screen id → the story that makes the whole route pass the touch rules on a phone.
// Delete your line once your route passes; a listed route that passes fails the sweep.
public static class TouchPendingRoutes
{
    public static readonly IReadOnlyDictionary<string, string> All = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // SHIP-736
        ["login"] = "SHIP-736",
        // SHIP-733
        ["profile"] = "SHIP-733",
        // SHIP-716
        ["storiesBacklog"] = "SHIP-716",
        // SHIP-722
        ["featureBoard"] = "SHIP-722",
        // SHIP-719
        ["storiesBoard"] = "SHIP-719",
        ["featuresBoard"] = "SHIP-719",
        ["epicsBoard"] = "SHIP-719",
        // SHIP-723
        ["sprints"] = "SHIP-723",
        // SHIP-724
        ["sprintBacklog"] = "SHIP-724",
        // SHIP-725
        ["taskboard"] = "SHIP-725",
        // SHIP-726
        ["capacity"] = "SHIP-726",
        // SHIP-727
        // SHIP-730
        ["retros"] = "SHIP-730",
        // SHIP-728
        // SHIP-729
        // SHIP-732
        ["projectSettings"] = "SHIP-732",
        // SHIP-889
    };
}
