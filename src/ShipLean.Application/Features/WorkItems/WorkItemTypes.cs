using ShipLean.Contracts.Refs;

namespace ShipLean.Application.Features.WorkItems;

// How a work item type reads in the database, on the wire and on the boards.
internal static class WorkItemTypes
{
    public static string Db(WorkItemTypeId type) => type.ToString().ToLowerInvariant();

    public static WorkItemTypeId Parse(string db) => Enum.Parse<WorkItemTypeId>(db, ignoreCase: true);

    public static TypeRef Ref(WorkItemTypeId type) => new(type, type == WorkItemTypeId.Story ? "User Story" : type.ToString());

    // The board level that shows the type; tasks live on the sprint taskboard, whose columns are states, not board columns.
    public static string? BoardLevel(WorkItemTypeId type) => type switch
    {
        WorkItemTypeId.Epic => "epics",
        WorkItemTypeId.Feature => "features",
        WorkItemTypeId.Story or WorkItemTypeId.Bug => "stories",
        _ => null,
    };

    // Epic <- Feature <- Feature | Story | Bug, and Story | Bug <- Task (the schema's ck_work_item_parent_type_allowed).
    public static bool IsAllowedParent(WorkItemTypeId type, WorkItemTypeId parent) => type switch
    {
        WorkItemTypeId.Feature => parent is WorkItemTypeId.Epic or WorkItemTypeId.Feature,
        WorkItemTypeId.Story or WorkItemTypeId.Bug => parent == WorkItemTypeId.Feature,
        WorkItemTypeId.Task => parent is WorkItemTypeId.Story or WorkItemTypeId.Bug,
        _ => false,
    };
}
