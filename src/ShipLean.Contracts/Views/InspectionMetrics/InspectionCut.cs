namespace ShipLean.Contracts.Views.InspectionMetrics;

// How the lead-time table is cut across the sprints (K2): not at all, per Feature, or per agent connection with people together.
public enum InspectionCut
{
    None,
    Feature,
    Who,
}
