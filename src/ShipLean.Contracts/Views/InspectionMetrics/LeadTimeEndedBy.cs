namespace ShipLean.Contracts.Views.InspectionMetrics;

// Where a task's lead time ended (design D1): its last commit's time, or its first move to Resolved when no commit has a time.
public enum LeadTimeEndedBy
{
    LastCommit,
    Resolved,
}
