namespace ShipLean.Contracts.Refs;

// What a work item type is called and which types sit at the story level (Story, Bug and Action item). Every lane asks here
// instead of naming the types itself.
public static class WorkItemTypeRules
{
    // The story-level type ids as the database stores them, for `= ANY(@storyLevel)` (a string[] at run time; AG0018 bans the array type here).
    public static IReadOnlyList<string> StoryLevelDb { get; } = ["story", "bug", "action"];

    public static string Name(WorkItemTypeId type) => type switch
    {
        WorkItemTypeId.Story => "User Story",
        WorkItemTypeId.Action => "Action item",
        _ => type.ToString(),
    };

    public static bool IsStoryLevel(WorkItemTypeId type) => type is WorkItemTypeId.Story or WorkItemTypeId.Bug or WorkItemTypeId.Action;
}
