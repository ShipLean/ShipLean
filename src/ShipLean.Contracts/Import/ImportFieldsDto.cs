namespace ShipLean.Contracts.Import;

public sealed record ImportFieldsDto(string StoryPointsFieldId, IReadOnlyList<StoryPointsFieldDto> Candidates, bool IncludeComponents, bool IncludeComments, bool IncludeAttachments, bool IncludeChangelog);
