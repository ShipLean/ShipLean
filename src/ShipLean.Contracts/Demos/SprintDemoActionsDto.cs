namespace ShipLean.Contracts.Demos;

// CreateWarning is IAgentConnectionWarnings' text for the project's agent connection, shown beside Create demo, or null.
public sealed record SprintDemoActionsDto(bool CanCreate, string? CreateBlockedMessage, bool CanRetryYouTube, string? CreateWarning);
