using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Persisted;

// The one place that keeps stored transitions in step with a project's states. Pure: no I/O, no clock.
public static class TransitionStates
{
    // Pass every state of the project, archived ones included. A live id refreshes its stored name. An archived (or unknown) id
    // re-binds to the live state of the transition's own type whose name matches (case-insensitive), with that state's id and
    // name. Anything else is left as it is, and so stays Invalid. A re-bind that would make the transition equal to another one
    // (same type, from and to) or send it from a state to itself is skipped. The order is kept, and normalising twice changes nothing more.
    public static IReadOnlyList<TransitionJson> Normalize(IReadOnlyList<TransitionJson> transitions, IReadOnlyCollection<TransitionState> statesInclArchived)
    {
        var live = statesInclArchived.Where(s => !s.Archived).ToList();
        var refreshed = transitions.Select(t => t with { From = Refresh(t.Type, t.From, live), To = Refresh(t.Type, t.To, live)! }).ToList();
        var taken = refreshed.Where(t => IsValid(t, live)).Select(Key).ToHashSet();
        var result = new List<TransitionJson>();
        foreach (var transition in refreshed)
        {
            var bound = transition with { From = Rebind(transition.Type, transition.From, live), To = Rebind(transition.Type, transition.To, live)! };
            var changed = !Equals(bound.From, transition.From) || !Equals(bound.To, transition.To);
            if (changed && bound.From?.StateId != bound.To.StateId && IsValid(bound, live) && taken.Add(Key(bound)))
            {
                result.Add(bound);
            }
            else
            {
                result.Add(transition);
            }
        }
        return result;
    }

    // Valid: the to state, and the from state when there is one, are live states of the transition's type. Pass live states only.
    public static bool IsValid(TransitionJson transition, IReadOnlyCollection<TransitionState> liveStates)
    {
        return Has(transition.Type, transition.To, liveStates) && (transition.From is null || Has(transition.Type, transition.From, liveStates));
    }

    private static bool Has(WorkItemTypeId type, StateRefJson state, IReadOnlyCollection<TransitionState> live)
        => live.Any(s => !s.Archived && s.Type == type && s.Id == state.StateId);

    private static StateRefJson? Refresh(WorkItemTypeId type, StateRefJson? state, List<TransitionState> live)
        => state is not null && live.FirstOrDefault(s => s.Type == type && s.Id == state.StateId) is { } found ? state with { Name = found.Name } : state;

    private static StateRefJson? Rebind(WorkItemTypeId type, StateRefJson? state, List<TransitionState> live)
    {
        if (state is null || live.Any(s => s.Type == type && s.Id == state.StateId))
        {
            return state;
        }
        var match = live.FirstOrDefault(s => s.Type == type && string.Equals(s.Name, state.Name, StringComparison.OrdinalIgnoreCase));
        return match is null ? state : new StateRefJson(match.Id, match.Name);
    }

    private static (WorkItemTypeId, Guid?, Guid) Key(TransitionJson t) => (t.Type, t.From?.StateId, t.To.StateId);
}
