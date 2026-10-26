using ShipLean.Contracts.Boards;
using ShipLean.Contracts.Persisted;

namespace ShipLean.Contracts.Views;

// GET of a sprint's Taskboard. ParentStates are the Story and Bug states for the row header's state picker; StyleRules, CardFields
// and Annotations are the Taskboard's settings as stored. Rows and PeopleRows carry the same tasks, grouped two ways (the grouping choice is a per-user preference).
public sealed record TaskboardViewDto(
    Guid SprintId,
    Guid BoardId,
    IReadOnlyList<TaskboardColumnDto> Columns,
    IReadOnlyList<TaskboardRowDto> Rows,
    IReadOnlyList<TaskboardPersonRowDto> PeopleRows,
    IReadOnlyList<BoardTypeStatesDto> ParentStates,
    IReadOnlyList<StyleRuleJson> StyleRules,
    IReadOnlyList<CardFieldSettingJson> CardFields,
    CardAnnotationsJson Annotations);
