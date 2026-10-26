namespace ShipLean.Contracts.Search;

// Which text of a source a search_doc row holds. It sets the tsvector weight (docs/search.md).
public enum SearchField
{
    Title,
    Tags,
    Body,
    Comment,
    Attachment,
    Project,
    SprintGoal,
    SprintName,
}
