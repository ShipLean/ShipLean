using ShipLean.Contracts.Persisted;

namespace ShipLean.Contracts.WorkItems;

// Webhook is the pull or merge request a webhook write came from ("GitHub acme/api#12"), null otherwise.
public sealed record RevisionSnapshot(int Rev, Guid? ChangedBy, DateTimeOffset ChangedAt, string? OriginalSystem, Guid? ImportJobId, IReadOnlyList<FieldChange> Changes, string? AgentName = null, string? Webhook = null)
{
    public RevisionOrigin Origin => (Webhook, OriginalSystem) switch
    {
        ({ }, _) => RevisionOrigin.Webhook,
        (null, null) => RevisionOrigin.User,
        _ => RevisionOrigin.Jira,
    };
}
