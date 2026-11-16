namespace ShipLean.Contracts.Demos;

// Numbers and AgentSaid were added for the phone design (SHIP-1810 P7 and P5); StatusMessage still carries the whole sentence.
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
    SprintDemoActionsDto Actions,
    SprintDemoNumbersDto Numbers,
    SprintDemoAgentSaidDto? AgentSaid);
