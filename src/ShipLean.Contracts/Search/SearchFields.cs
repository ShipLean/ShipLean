namespace ShipLean.Contracts.Search;

// Which fields get a vector (docs/search.md). The rest are lexical only: never stale under the model rule, never pending.
public static class SearchFields
{
    public static bool IsEmbedded(SearchField field) => field is SearchField.Title or SearchField.Body or SearchField.Comment
        or SearchField.Attachment or SearchField.SprintGoal;
}
