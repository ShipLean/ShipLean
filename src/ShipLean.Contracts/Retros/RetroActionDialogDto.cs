namespace ShipLean.Contracts.Retros;

public sealed record RetroActionDialogDto(IReadOnlyList<RetroPointDto> Selected, IReadOnlyList<RetroPointDto> Offered, IReadOnlyList<RetroActionRefDto> OpenActions, RetroThemeRefDto? SuggestedTheme);
