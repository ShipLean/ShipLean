using ShipLean.Contracts.Boards;
using ShipLean.Contracts.Criteria;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;

namespace ShipLean.Application.Features.Boards.Settings;

// The checks a board settings put makes on the merged document, before it is stored (see IBoardSettingsStore.PutAsync).
internal static class BoardSettingsValidation
{
    public const int MaxExtraFields = 10;

    public static void Check(BoardLevel level, BoardSettingsJson settings, IReadOnlyDictionary<WorkItemTypeId, List<StateRef>> statesByType, ICriteriaEvaluator criteria)
    {
        foreach (var lane in settings.Swimlanes.Where(l => l.Criteria is not null))
        {
            CheckCriteria($"Lane {lane.Name}", lane.Criteria!, criteria);
        }
        foreach (var rule in settings.StyleRules)
        {
            CheckCriteria($"Rule {rule.Name}", rule.Criteria, criteria);
        }
        foreach (var fields in settings.CardFields)
        {
            CheckExtraFields(fields);
        }
        if (level == BoardLevel.Taskboard)
        {
            return;
        }
        if (settings.Swimlanes.Count(l => l.Kind == SwimlaneKind.Default) != 1)
        {
            throw ApiException.Validation("A board needs exactly one default lane.");
        }
        CheckColumns(level, settings.Columns, statesByType);
    }

    // Every list, nested list and required string is present. Implicit [Required] is off, so a body can carry nulls the
    // types do not allow; the rules above and the merge assume they do not.
    public static void CheckWellFormed(BoardSettingsJson? settings, string what)
    {
        if (settings is null || settings.Columns is null || settings.Swimlanes is null || settings.CardFields is null
            || settings.StyleRules is null || settings.TagColours is null || settings.Annotations is null)
        {
            throw Malformed(what);
        }
        var wellFormed =
            settings.Columns.All(c => c is not null && c.Id is not null && c.Name is not null && c.DefinitionOfDone is not null
                && c.StateMappings is not null && c.StateMappings.All(m => m is not null))
            && settings.Swimlanes.All(l => l is not null && l.Id is not null && l.Name is not null && (l.Criteria is null || CriteriaWellFormed(l.Criteria)))
            && settings.CardFields.All(f => f is not null && f.ExtraFields is not null && f.ExtraFields.All(e => e is not null))
            && settings.StyleRules.All(r => r is not null && r.Id is not null && r.Name is not null && r.Criteria is not null && CriteriaWellFormed(r.Criteria))
            && settings.TagColours.All(t => t is not null && t.Tag is not null);
        if (!wellFormed)
        {
            throw Malformed(what);
        }
    }

    private static bool CriteriaWellFormed(CriteriaJson criteria)
        => criteria.Clauses is not null && criteria.Clauses.All(c => c is not null && c.Field is not null);

    private static ApiException Malformed(string what)
        => ApiException.Validation($"The {what} board settings are missing a required list or value.");

    private static void CheckCriteria(string what, CriteriaJson json, ICriteriaEvaluator evaluator)
    {
        var errors = evaluator.Validate(json);
        if (errors.Count > 0)
        {
            throw ApiException.Validation($"{what}: {errors[0].Message}");
        }
    }

    private static void CheckExtraFields(CardFieldSettingJson fields)
    {
        if (fields.ExtraFields.Count > MaxExtraFields)
        {
            throw ApiException.Validation($"A {fields.Type} card shows at most {MaxExtraFields} extra fields.");
        }
        if (fields.ExtraFields.Distinct().Count() != fields.ExtraFields.Count)
        {
            throw ApiException.Validation($"A {fields.Type} card lists an extra field twice.");
        }
        foreach (var id in fields.ExtraFields)
        {
            var field = FieldIds.All.FirstOrDefault(f => f.Id == id);
            if (field is null || !field.UsedIn.Contains(FieldUse.CardField) || !field.AppliesTo.Contains(fields.Type))
            {
                throw ApiException.Validation($"{id} cannot be shown on a {fields.Type} card.");
            }
        }
    }

    private static void CheckColumns(BoardLevel level, IReadOnlyList<BoardColumnJson> columns, IReadOnlyDictionary<WorkItemTypeId, List<StateRef>> statesByType)
    {
        if (columns.Count > 0 && (columns[0].WipLimit is not null || columns[0].Split || columns[^1].WipLimit is not null || columns[^1].Split))
        {
            throw ApiException.Validation("The first and last columns have no WIP limit and cannot be split.");
        }
        var shown = BoardDefaultSettings.TypesShown(level);
        foreach (var column in columns)
        {
            foreach (var type in shown)
            {
                var mappings = column.StateMappings.Where(m => m.Type == type).ToList();
                if (mappings.Count != 1 || column.StateMappings.Count != shown.Count)
                {
                    throw ApiException.Validation($"Column {column.Name} needs exactly one {type} state.");
                }
                var state = statesByType.GetValueOrDefault(type)?.FirstOrDefault(s => s.Id == mappings[0].StateId);
                if (state is null || state.Category == StateCategory.Removed)
                {
                    throw ApiException.Validation($"Column {column.Name} maps a state that is not a live {type} state.");
                }
            }
        }
    }
}
