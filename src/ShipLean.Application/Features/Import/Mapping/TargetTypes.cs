using ShipLean.Contracts.Import;
using ShipLean.Contracts.Import.Jira;
using ShipLean.Contracts.Refs;

namespace ShipLean.Application.Features.Import.Mapping;

// Conversions between the mapping's vocabulary and the work item model's.
public static class TargetTypes
{
    // Null for Skip.
    public static WorkItemTypeId? WorkItemType(ImportTypeTarget target) => target switch
    {
        ImportTypeTarget.Epic => WorkItemTypeId.Epic,
        ImportTypeTarget.Feature => WorkItemTypeId.Feature,
        ImportTypeTarget.Story => WorkItemTypeId.Story,
        ImportTypeTarget.Bug => WorkItemTypeId.Bug,
        ImportTypeTarget.Task => WorkItemTypeId.Task,
        _ => null,
    };

    public static StateCategory CategoryOf(JiraStatusCategory category) => category switch
    {
        JiraStatusCategory.New => StateCategory.Proposed,
        JiraStatusCategory.Indeterminate => StateCategory.InProgress,
        _ => StateCategory.Completed,
    };
}
