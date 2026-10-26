using System.Text.Json;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.Boards.View;

// The field writes behind a drop into or out of a criteria lane (decision: "=" clauses are written; dropping out writes the
// default). Only fields a card drop can set are writable; a lane with any other clause or operator refuses drops.
public static class LaneWrites
{
    private const int DefaultPriority = 2;

    private static readonly string[] Writable = [FieldIds.Priority, FieldIds.Severity, FieldIds.Points, FieldIds.Assignee, FieldIds.Sprint];

    public static bool CanDropInto(CriteriaJson criteria)
        => criteria.Clauses.Count > 0 && criteria.Clauses.All(c => c.Op == CriteriaOp.Equal && Writable.Contains(c.Field));

    // Every clause's value, written to its field on top of the changes so far (so the lane entered wins over a default written
    // for the lane left). "@me" is the person moving the card.
    public static WorkItemChanges Entering(CriteriaJson criteria, Guid me, WorkItemChanges? start = null)
        => criteria.Clauses.Aggregate(start ?? new WorkItemChanges(), (changes, clause) => clause.Field switch
        {
            FieldIds.Priority => changes with { Priority = new FieldSet<int>(clause.Value.GetInt32()) },
            FieldIds.Severity => changes with { Severity = new FieldSet<int?>(clause.Value.GetInt32()) },
            FieldIds.Points => changes with { Points = new FieldSet<decimal?>(clause.Value.GetDecimal()) },
            FieldIds.Assignee => changes with { Assignee = new FieldSet<Guid?>(Id(clause.Value, me)) },
            FieldIds.Sprint => changes with { Sprint = new FieldSet<Guid?>(Id(clause.Value, me)) },
            _ => throw ApiException.Validation($"A card can't be dropped into a lane that sets {clause.Field}."),
        });

    // The default of every field the lanes write: Priority 2, everything else cleared.
    public static WorkItemChanges Leaving(IEnumerable<CriteriaJson> lanes)
        => lanes.SelectMany(l => l.Clauses).Select(c => c.Field).Distinct().Aggregate(new WorkItemChanges(), (changes, field) => field switch
        {
            FieldIds.Priority => changes with { Priority = new FieldSet<int>(DefaultPriority) },
            FieldIds.Severity => changes with { Severity = new FieldSet<int?>(null) },
            FieldIds.Points => changes with { Points = new FieldSet<decimal?>(null) },
            FieldIds.Assignee => changes with { Assignee = new FieldSet<Guid?>(null) },
            FieldIds.Sprint => changes with { Sprint = new FieldSet<Guid?>(null) },
            _ => throw ApiException.Validation($"A card can't be dragged out of a lane that is set by {field}."),
        });

    private static Guid Id(JsonElement value, Guid me)
        => value.GetString() == "@me" ? me : Guid.Parse(value.GetString()!);
}
