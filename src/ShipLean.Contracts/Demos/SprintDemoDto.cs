namespace ShipLean.Contracts.Demos;

public sealed record SprintDemoDto(
    Guid SprintId,
    string SprintName,
    SprintDemoStatus Status,
    string? StatusMessage,
    SprintDemoRunDto? Run,
    SprintDemoVideoDto? Video,
    IReadOnlyList<SprintDemoChapterDto> Chapters,
    IReadOnlyList<SprintDemoFeatureDto> Features,
    string? ScriptHtml,
    SprintDemoYouTubeDto YouTube,
    SprintDemoActionsDto Actions);
