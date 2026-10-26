namespace ShipLean.Contracts.Views.Search;

// Which text of the item matched. Description is the item's body (description, acceptance criteria, repro steps).
public enum SearchMatchedIn
{
    Title,
    Tags,
    Description,
    Comment,
    Attachment,
}
