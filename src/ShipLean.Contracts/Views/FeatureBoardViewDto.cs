using ShipLean.Contracts.Boards;
using ShipLean.Contracts.Persisted;

namespace ShipLean.Contracts.Views;

// GET of a Feature's board: the Taskboard scoped to one Feature. Columns, PeopleRows, ParentStates and the settings are the
// project's Taskboard, as for TaskboardViewDto. Sections are the board's Feature first and then each nested Feature, depth
// first; Rows is flat, section then row order, and each row names its section in FeatureId. PeopleRows cover the whole tree.
public sealed record FeatureBoardViewDto(
    Guid FeatureId,
    Guid BoardId,
    FeatureBoardHeaderDto Header,
    IReadOnlyList<TaskboardColumnDto> Columns,
    IReadOnlyList<FeatureBoardSectionDto> Sections,
    IReadOnlyList<FeatureBoardRowDto> Rows,
    IReadOnlyList<TaskboardPersonRowDto> PeopleRows,
    IReadOnlyList<BoardTypeStatesDto> ParentStates,
    IReadOnlyList<StyleRuleJson> StyleRules,
    IReadOnlyList<CardFieldSettingJson> CardFields,
    CardAnnotationsJson Annotations);
