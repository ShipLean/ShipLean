namespace ShipLean.Contracts.Search;

// What a text source says about one source. ItemTitle is the title the writer puts in front of every chunk of a
// Body, Comment or Attachment field (null for project and sprint sources). WorkItemId is null for project and sprint sources.
public sealed record SearchSourceText(Guid SourceId, Guid ProjectId, Guid? WorkItemId, string? ItemTitle, IReadOnlyList<SearchFieldText> Fields);
