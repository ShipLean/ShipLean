namespace ShipLean.Contracts.Views.InspectionMetrics;

// Which sprints the view shows (K3): the last 6 started (the running one included), this calendar quarter, last calendar quarter,
// or every sprint. A sprint belongs to the quarter its end date falls in (design D7).
public enum InspectionRange
{
    Last6,
    Quarter,
    LastQuarter,
    All,
}
