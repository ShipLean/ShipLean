namespace ShipLean.Contracts.Search;

// What a search_doc row was built from. The column values are in SearchDocColumns.
public enum SearchSourceKind
{
    Item,
    Comment,
    Attachment,
    Project,
    Sprint,
}
