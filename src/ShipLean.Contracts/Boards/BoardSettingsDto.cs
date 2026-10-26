using ShipLean.Contracts.Persisted;

namespace ShipLean.Contracts.Boards;

// One shape for the level boards and the Taskboard. States lists only the types the board shows (Taskboard: task, story, bug).
public sealed record BoardSettingsDto(
    Guid Id,
    Guid ProjectId,
    BoardLevel Level,
    BoardSettingsJson Settings,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<BoardTypeStatesDto> States,
    IReadOnlyList<BoardWarningDto> Warnings);
