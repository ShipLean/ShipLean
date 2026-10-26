namespace ShipLean.Contracts.Persisted;

public enum ImportJobState
{
    Queued,
    Running,
    Done,
    DoneWithErrors,
    Stopped,
    Failed,
}
