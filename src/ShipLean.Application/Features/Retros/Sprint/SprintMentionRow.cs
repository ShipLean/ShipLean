namespace ShipLean.Application.Features.Retros.Sprint;

// One live point of a theme in a sprint of the window.
internal sealed record SprintMentionRow(Guid ThemeId, Guid SprintId, string Category);
