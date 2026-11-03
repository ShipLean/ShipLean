using ShipLean.Contracts.Usage;

namespace ShipLean.WebApi.Features.Mcp.Usage;

// The usage event each MCP tool emits (the MCP tools column of the event catalogue). A tool with no entry here is not tracked,
// and a test fails when a tool is missing.
public static class ToolUsageMap
{
    public static readonly IReadOnlyDictionary<string, string> Events = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["list_projects"] = UsageEventNames.ProjectsListed,
        ["create_feature"] = UsageEventNames.WorkItemCreated,
        ["capture_work"] = UsageEventNames.WorkCaptured,
        ["break_down"] = UsageEventNames.WorkItemBrokenDown,
        ["get_work_item"] = UsageEventNames.WorkItemViewed,
        ["refine_work_item"] = UsageEventNames.WorkItemUpdated,
        ["set_status"] = UsageEventNames.WorkItemStatusSet,
        ["update_progress"] = UsageEventNames.WorkItemProgressUpdated,
        ["move_to_sprint"] = UsageEventNames.WorkItemMovedToSprint,
        ["reorder"] = UsageEventNames.WorkItemReordered,
        ["pick_up"] = UsageEventNames.TaskPickedUp,
        ["hand_back"] = UsageEventNames.TaskHandedBack,
        ["report_task"] = UsageEventNames.TaskReported,
        ["find_work_to_pick_up"] = UsageEventNames.PickupListed,
        ["get_brief"] = UsageEventNames.BriefViewed,
        ["capture_plan"] = UsageEventNames.PlanCaptured,
        ["plan_status"] = UsageEventNames.PlanStatusViewed,
        ["planning_guide"] = UsageEventNames.PlanningGuideViewed,
        ["feature_progress"] = UsageEventNames.FeatureProgressViewed,
        ["review_backlog"] = UsageEventNames.BacklogReviewed,
        ["whats_changed_since"] = UsageEventNames.ChangesViewed,
        ["search"] = UsageEventNames.SearchPerformed,
        ["comment"] = UsageEventNames.CommentAdded,
        ["attach_markdown"] = UsageEventNames.AttachmentAdded,
        ["attach_file"] = UsageEventNames.AttachmentAdded,
        ["read_attachment"] = UsageEventNames.AttachmentViewed,
        ["link"] = UsageEventNames.LinkAdded,
        ["report_link_state"] = UsageEventNames.LinkStateReported,
        ["create_sprint"] = UsageEventNames.SprintCreated,
        ["plan_sprint"] = UsageEventNames.SprintPlanned,
        ["end_sprint"] = UsageEventNames.SprintEnded,
        ["sprint_status"] = UsageEventNames.SprintStatusViewed,
        ["sprint_review"] = UsageEventNames.SprintReviewed,
        ["log_retrospective"] = UsageEventNames.RetroPointAdded,
        ["list_retrospectives"] = UsageEventNames.RetroListed,
        ["decide_retro_actions"] = UsageEventNames.RetroActionsDecided,
    };
}
