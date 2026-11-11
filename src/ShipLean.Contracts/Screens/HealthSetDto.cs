using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Screens;

// When a Feature's health was last set, and by whom. Agent is the agent stamp, null for a change made in the app.
public sealed record HealthSetDto(DateTimeOffset At, string AtLabel, UserRef? By, string? Agent);
