using ShipLean.Contracts.Retros;

namespace ShipLean.TestSupport.Retros;

// One point for RetroSeed. AuthorId defaults to the retro's author; Position to the next one in the retro.
// NotNeededReason marks the point Not needed: DecidedById defaults to the point's author and DecidedAt to now.
public sealed record RetroSeedPoint(RetroCategory Category, string Text, string? RootCause = null, Guid? ThemeId = null, RetroThemeSetBy? ThemeSetBy = null, Guid? AuthorId = null, string? AgentName = null, int? Position = null, bool Archived = false, DateTimeOffset? CreatedAt = null, string? NotNeededReason = null, Guid? DecidedById = null, string? DecidedAgentName = null, DateTimeOffset? DecidedAt = null);
