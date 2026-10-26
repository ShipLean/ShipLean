using ShipLean.Contracts.Import;
using ShipLean.Contracts.Import.Jira;

namespace ShipLean.Application.Features.Import.Mapping;

// The suggested ShipLean type for a Jira issue type, and the reason shown beside it. Unrecognised types are reported (Recognised false).
internal static class TypeDefaults
{
    public static (ImportTypeTarget Target, string Note, bool Recognised) For(JiraIssueType type) => type.Name.Trim().ToLowerInvariant() switch
    {
        "epic" => (ImportTypeTarget.Feature, "Epics become Features", true),
        "initiative" => (ImportTypeTarget.Epic, "Initiatives become Epics", true),
        "story" or "user story" => (ImportTypeTarget.Story, "Stories become User Stories", true),
        "bug" => (ImportTypeTarget.Bug, "Bugs stay Bugs", true),
        "sub-task" or "subtask" => (ImportTypeTarget.Task, "Sub-tasks become Tasks", true),
        _ when type.Subtask => (ImportTypeTarget.Task, "Sub-tasks become Tasks", true),
        "task" => (ImportTypeTarget.Story, "Tasks become User Stories", true),
        _ => (ImportTypeTarget.Story, "No ShipLean equivalent: imported as a User Story", false),
    };
}
