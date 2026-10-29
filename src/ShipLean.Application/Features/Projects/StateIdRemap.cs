using System.Text.Json;
using System.Text.Json.Nodes;
using ShipLean.Contracts.Persisted;

namespace ShipLean.Application.Features.Projects;

// Rewrites a copied board document for the copy. Column mappings must all name states the source has (a mapping to a deleted
// state is a bug, so it throws). In lane and style-rule criteria, State clauses are remapped, and ids the copy has no state for
// are dropped (criteria validation checks only value types, so a rule can name a deleted state); Sprint and WorkItem clauses
// point at the source's own sprints and items, so they are dropped. A clause left with nothing to match is dropped, and a
// lane or style rule whose clauses all went is dropped too, since an empty "all" would match every card. A transition's
// state refs are remapped the same way, and one naming a state the copy lacks is dropped.
internal static class StateIdRemap
{
    private static readonly Dictionary<string, FieldKind> Kinds = FieldIds.All.ToDictionary(f => f.Id, f => f.Kind);

    public static BoardSettingsJson Apply(BoardSettingsJson settings, IReadOnlyDictionary<Guid, Guid> map)
        => settings with
        {
            Columns = [.. settings.Columns.Select(c => c with { StateMappings = [.. c.StateMappings.Select(m => m with { StateId = Map(map, m.StateId) })] })],
            Swimlanes = [.. settings.Swimlanes.Select(l => l.Criteria is null ? l : Lane(l, map)).OfType<SwimlaneJson>()],
            StyleRules = [.. settings.StyleRules.Select(r => Rule(r, map)).OfType<StyleRuleJson>()],
            Transitions = [.. settings.Transitions.Select(t => Transition(t, map)).OfType<TransitionJson>()],
        };

    // A transition naming a state the copy has no state for (an archived one, which a copy leaves behind) is dropped.
    private static TransitionJson? Transition(TransitionJson transition, IReadOnlyDictionary<Guid, Guid> map)
    {
        if (!map.TryGetValue(transition.To.StateId, out var to))
        {
            return null;
        }
        if (transition.From is null)
        {
            return transition with { To = transition.To with { StateId = to } };
        }
        return map.TryGetValue(transition.From.StateId, out var from)
            ? transition with { To = transition.To with { StateId = to }, From = transition.From with { StateId = from } }
            : null;
    }

    private static SwimlaneJson? Lane(SwimlaneJson lane, IReadOnlyDictionary<Guid, Guid> map)
        => Criteria(lane.Criteria!, map) is { } criteria ? lane with { Criteria = criteria } : null;

    private static StyleRuleJson? Rule(StyleRuleJson rule, IReadOnlyDictionary<Guid, Guid> map)
        => Criteria(rule.Criteria, map) is { } criteria ? rule with { Criteria = criteria } : null;

    // Null when the criteria had clauses and none survived.
    private static CriteriaJson? Criteria(CriteriaJson criteria, IReadOnlyDictionary<Guid, Guid> map)
    {
        var clauses = criteria.Clauses.Select(c => Clause(c, map)).OfType<CriteriaClause>().ToList();
        return clauses.Count == 0 && criteria.Clauses.Count > 0 ? null : criteria with { Clauses = clauses };
    }

    private static CriteriaClause? Clause(CriteriaClause clause, IReadOnlyDictionary<Guid, Guid> map)
        => Kinds.GetValueOrDefault(clause.Field) switch
        {
            FieldKind.Sprint or FieldKind.WorkItem => null,
            FieldKind.State => Value(clause.Value, map) is { } value ? clause with { Value = value } : null,
            _ => clause,
        };

    // A State clause's value is one id (string) or, for "in", an array of ids. Null when no id is left.
    private static JsonElement? Value(JsonElement value, IReadOnlyDictionary<Guid, Guid> map)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String when value.TryGetGuid(out var id):
                return map.TryGetValue(id, out var copied) ? JsonSerializer.SerializeToElement(copied.ToString()) : null;
            case JsonValueKind.Array:
                var kept = value.EnumerateArray()
                    .Where(e => e.ValueKind != JsonValueKind.String || !e.TryGetGuid(out var id) || map.ContainsKey(id))
                    .Select(e => e.ValueKind == JsonValueKind.String && e.TryGetGuid(out var id) ? JsonValue.Create(map[id].ToString()) : JsonSerializer.SerializeToNode(e))
                    .ToArray();
                return kept.Length == 0 ? null : JsonSerializer.SerializeToElement(new JsonArray(kept));
            default:
                return value;
        }
    }

    private static Guid Map(IReadOnlyDictionary<Guid, Guid> map, Guid id)
        => map.TryGetValue(id, out var copied) ? copied : throw new InvalidOperationException($"Board settings map a column to state {id}, which the project being copied does not have.");
}
