namespace ShipLean.Application.Features.Retros.Sprint;

// An action item on a theme whose state is completed.
internal sealed record SprintClosedActionRow(Guid ItemId, Guid ThemeId);
