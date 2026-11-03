using ShipLean.Contracts.Retros;
using ShipLean.Contracts.Retros.Decide;

namespace ShipLean.Application.Features.Retros.Decide;

// Decision is null for a wentWell point (K2).
public sealed record DecideScopePoint(Guid Id, string Text, RetroCategory Category, RetroDecision? Decision, string? ThemeName, bool ThemeByPerson)
{
    public bool Decided => Decision is RetroDecision.Actioned or RetroDecision.NotNeeded;

    public bool Undecided => Decision == RetroDecision.Undecided;
}
