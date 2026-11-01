using ShipLean.Contracts.Retros;

namespace ShipLean.Application.Features.Retros.Search;

internal sealed record SearchTheme(Guid Id, string Name, RetroTone Tone)
{
    public RetroThemeRefDto ToRef() => new(Id, Name, Tone);
}
