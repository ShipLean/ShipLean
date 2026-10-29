
namespace ShipLean.Contracts.Persisted;

public sealed record BoardSettingsJson(IReadOnlyList<BoardColumnJson> Columns, IReadOnlyList<SwimlaneJson> Swimlanes, IReadOnlyList<CardFieldSettingJson> CardFields, IReadOnlyList<StyleRuleJson> StyleRules, IReadOnlyList<TagColourJson> TagColours, CardAnnotationsJson Annotations, CardReordering? CardReordering, IReadOnlyList<TransitionJson> Transitions);
