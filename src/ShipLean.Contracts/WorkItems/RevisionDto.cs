using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.WorkItems;

// AgentName is "Claude Code (claude.ai)" or "Claude Code (unverified)" when an agent made the change for ChangedBy, otherwise null.
// Webhook is the pull or merge request a webhook change came from ("GitHub acme/api#12"; ChangedBy is null), otherwise null.
public sealed record RevisionDto(int Rev, UserRef? ChangedBy, DateTimeOffset ChangedAt, string ChangedAtLabel, RevisionOrigin Origin, IReadOnlyList<FieldChange> Changes, string? AgentName = null, string? Webhook = null);
