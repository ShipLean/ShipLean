namespace ShipLean.Contracts.Persisted;

public enum LinkType
{
    Predecessor,
    Successor,
    BlockedBy,
    Related,
    Duplicate,
    Url,

    // From a Bug to the Task that introduced it (SHIP-1398 item D18); shown as Caused by on the bug and Caused on the task.
    CausedBy,
}
