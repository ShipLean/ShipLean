namespace ShipLean.Contracts.Search;

// The text stored in search_doc.source_kind and search_doc.field.
public static class SearchDocColumns
{
    public static string Kind(SearchSourceKind kind) => kind switch
    {
        SearchSourceKind.Item => "item",
        SearchSourceKind.Comment => "comment",
        SearchSourceKind.Attachment => "attachment",
        SearchSourceKind.Project => "project",
        SearchSourceKind.Sprint => "sprint",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static string Field(SearchField field) => field switch
    {
        SearchField.Title => "title",
        SearchField.Tags => "tags",
        SearchField.Body => "body",
        SearchField.Comment => "comment",
        SearchField.Attachment => "attachment",
        SearchField.Project => "project",
        SearchField.SprintGoal => "sprint_goal",
        SearchField.SprintName => "sprint_name",
        _ => throw new ArgumentOutOfRangeException(nameof(field)),
    };

    public static SearchSourceKind ParseKind(string column) => column switch
    {
        "item" => SearchSourceKind.Item,
        "comment" => SearchSourceKind.Comment,
        "attachment" => SearchSourceKind.Attachment,
        "project" => SearchSourceKind.Project,
        "sprint" => SearchSourceKind.Sprint,
        _ => throw new ArgumentOutOfRangeException(nameof(column), column, "Unknown search_doc source_kind."),
    };

    public static SearchField ParseField(string column) => column switch
    {
        "title" => SearchField.Title,
        "tags" => SearchField.Tags,
        "body" => SearchField.Body,
        "comment" => SearchField.Comment,
        "attachment" => SearchField.Attachment,
        "project" => SearchField.Project,
        "sprint_goal" => SearchField.SprintGoal,
        "sprint_name" => SearchField.SprintName,
        _ => throw new ArgumentOutOfRangeException(nameof(column), column, "Unknown search_doc field."),
    };
}
