namespace ShipLean.Contracts.Refs;

// What a work item type is called and which types sit at the story level (Story, Bug and Action item). Every lane asks here
// instead of naming the types itself.
public static class WorkItemTypeRules
{
    // The story-level type ids as the database stores them, for `= ANY(@storyLevel)` (Dapper/Npgsql bind it as text[]; it is not a string[] instance, because AG0018 bans the public array type).
    public static IReadOnlyList<string> StoryLevelDb { get; } = ["story", "bug", "action"];

    public static string Name(WorkItemTypeId type) => type switch
    {
        WorkItemTypeId.Story => "User Story",
        WorkItemTypeId.Action => "Action item",
        WorkItemTypeId.Bau => "BAU",
        _ => type.ToString(),
    };

    // Stories and Bugs: the types a duplicate check compares against (Action items are story-level but not compared).
    public static bool IsStoryOrBug(WorkItemTypeId type) => type is WorkItemTypeId.Story or WorkItemTypeId.Bug;

    public static bool IsStoryLevel(WorkItemTypeId type) => type is WorkItemTypeId.Story or WorkItemTypeId.Bug or WorkItemTypeId.Action;

    // A BAU item is made by ShipLean and never shows in a list or search result; it is reached by its key, its id or a link.
    public const string HiddenDb = "bau";

    public const string NotHiddenSql = "w.type <> 'bau'";

    public static bool IsHidden(WorkItemTypeId type) => type is WorkItemTypeId.Bau;
}
