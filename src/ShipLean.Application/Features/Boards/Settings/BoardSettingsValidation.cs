using System.Text.Json;
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
    public const int MaxPromptLength = 20_000;
    public const string AgentActionKind = "agent";

    // stored: the transitions the board holds now. One of them, sent back unchanged, may still name an archived state.
    public static void Check(
        BoardLevel level, BoardSettingsJson settings, IReadOnlyDictionary<WorkItemTypeId, List<StateRef>> statesByType, ICriteriaEvaluator criteria,
        IReadOnlyList<TransitionJson> stored)
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
        CheckTransitions(level, settings.Transitions, stored, statesByType);
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
        // Transitions may be missing (a client from before they existed): the merge then keeps the stored ones.
        if (settings.Transitions is not null && !settings.Transitions.All(TransitionWellFormed))
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

    private static bool TransitionWellFormed(TransitionJson? t)
        => t is not null && t.Id is not null && t.To is not null && t.To.Name is not null && (t.From is null || t.From.Name is not null)
            && t.Actions is not null && t.Actions.All(a => a is not null && a.Id is not null && a.Kind is not null && a.Prompt is not null);

    private static bool CriteriaWellFormed(CriteriaJson criteria)
        => criteria.Clauses is not null && criteria.Clauses.All(c => c is not null && c.Field is not null);

    private static ApiException Malformed(string what)
        => ApiException.Validation($"The {what} board settings are missing a required list or value.");

    // The rules on the transitions of the merged document. A transition equal to the stored one with the same id may keep archived
    // states; a new or edited one must name live states of its type. Connections are checked by the store (they need a read).
    private static void CheckTransitions(
        BoardLevel level, IReadOnlyList<TransitionJson> transitions, IReadOnlyList<TransitionJson> stored, IReadOnlyDictionary<WorkItemTypeId, List<StateRef>> statesByType)
    {
        var allowed = level switch
        {
            BoardLevel.Stories => new[] { WorkItemTypeId.Story, WorkItemTypeId.Bug },
            BoardLevel.Features => [WorkItemTypeId.Feature],
            _ => Array.Empty<WorkItemTypeId>(),
        };
        if (allowed.Length == 0 && transitions.Count > 0)
        {
            throw ApiException.Validation($"A {level} board has no transitions.");
        }
        var ids = new HashSet<string>();
        var actionIds = new HashSet<string>();
        var keys = new HashSet<(WorkItemTypeId, Guid?, Guid)>();
        foreach (var transition in transitions)
        {
            if (string.IsNullOrWhiteSpace(transition.Id))
            {
                throw ApiException.Validation("A transition needs an id.");
            }
            if (!ids.Add(transition.Id))
            {
                throw ApiException.Validation("Two transitions share an id.");
            }
            if (!allowed.Contains(transition.Type))
            {
                throw ApiException.Validation($"A {transition.Type} transition does not belong on a {level} board.");
            }
            if (transition.From is not null && transition.From.StateId == transition.To.StateId)
            {
                throw ApiException.Validation("A transition cannot go from a state to the same state.");
            }
            if (!keys.Add((transition.Type, transition.From?.StateId, transition.To.StateId)))
            {
                throw ApiException.Validation($"A {transition.Type} transition from {transition.From?.Name ?? "Any state"} to {transition.To.Name} is listed twice.");
            }
            CheckActions(transition, actionIds);
            // Only a ref that differs from the stored transition's must be live: editing a prompt of an Invalid transition saves.
            var before = stored.FirstOrDefault(t => t.Id == transition.Id);
            var live = statesByType.GetValueOrDefault(transition.Type) ?? [];
            var changedRefs = new[] { (Now: transition.From, Was: before?.From), (Now: (StateRefJson?)transition.To, Was: before?.To) }
                .Where(r => r.Now is not null && (before is null || r.Now != r.Was))
                .Select(r => r.Now!);
            var missing = changedRefs.FirstOrDefault(state => live.All(s => s.Id != state.StateId));
            if (missing is not null)
            {
                throw ApiException.Validation($"{missing.Name} is not a live {transition.Type} state.");
            }
        }
    }

    private static void CheckActions(TransitionJson transition, HashSet<string> actionIds)
    {
        if (transition.Actions.Count == 0)
        {
            throw ApiException.Validation("A transition needs at least one action.");
        }
        foreach (var action in transition.Actions)
        {
            if (string.IsNullOrWhiteSpace(action.Id))
            {
                throw ApiException.Validation("An action needs an id.");
            }
            if (!actionIds.Add(action.Id))
            {
                throw ApiException.Validation("Two actions share an id.");
            }
            if (action.Kind != AgentActionKind)
            {
                throw ApiException.Validation($"{action.Kind} is not an action a transition can run.");
            }
            if (action.Prompt.Length > MaxPromptLength)
            {
                throw ApiException.Validation($"A prompt is at most {MaxPromptLength:N0} characters.");
            }
        }
    }

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
