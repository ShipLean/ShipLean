namespace ShipLean.Contracts.Screens;

// One work item in the digest with what happened to it. Type is the work item type id ("feature", "story").
public sealed record DigestItemDto(Guid Id, string Key, string Type, string Title, IReadOnlyList<DigestLineDto> Lines);
