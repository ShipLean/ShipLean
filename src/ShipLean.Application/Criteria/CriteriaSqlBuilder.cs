using System.Globalization;
using System.Text.Json;
using ShipLean.Contracts.Criteria;
using ShipLean.Contracts.Persisted;

namespace ShipLean.Application.Criteria;

// Writes the WHERE fragment of validated criteria over work_item under an alias. Field ids map to a fixed whitelist of column
// expressions; every value goes into a parameter named from the per-call prefix, never into the text.
internal sealed class CriteriaSqlBuilder(string alias, string prefix, Guid? me)
{
    private readonly Dictionary<string, object?> _parameters = [];

    public static CriteriaSql Build(CriteriaJson criteria, string alias, string prefix, Guid? me)
    {
        var builder = new CriteriaSqlBuilder(alias, prefix, me);
        var joiner = criteria.Match == CriteriaMatch.All ? " AND " : " OR ";
        var where = "(" + string.Join(joiner, criteria.Clauses.Select(builder.Clause)) + ")";
        return new CriteriaSql(where, builder._parameters);
    }

    // A clause that is false, never null, so NOT and OR behave.
    private string Clause(CriteriaClause clause)
    {
        var definition = FieldIds.All.First(f => f.Id == clause.Field);
        var column = Column(clause.Field);
        return $"coalesce(({Test(definition.Kind, column, clause)}), false)";
    }

    private string Test(FieldKind kind, string column, CriteriaClause clause)
    {
        var text = kind == FieldKind.Text;
        return clause.Op switch
        {
            CriteriaOp.IsEmpty => kind == FieldKind.Tags ? $"coalesce(cardinality({column}), 0) = 0" : $"{column} IS NULL",
            CriteriaOp.IsNotEmpty => kind == FieldKind.Tags ? $"coalesce(cardinality({column}), 0) > 0" : $"{column} IS NOT NULL",
            CriteriaOp.Equal => text ? $"lower({column}) = lower({Add(kind, clause.Value)})" : $"{column} = {Add(kind, clause.Value)}",
            CriteriaOp.NotEqual => text
                ? $"({column} IS NULL OR lower({column}) <> lower({Add(kind, clause.Value)}))"
                : $"{column} IS DISTINCT FROM {Add(kind, clause.Value)}",
            CriteriaOp.GreaterThan => $"{column} > {Add(kind, clause.Value)}",
            CriteriaOp.LessThan => $"{column} < {Add(kind, clause.Value)}",
            CriteriaOp.In => In(kind, column, clause.Value),
            CriteriaOp.Contains => kind == FieldKind.Tags ? HasTag(column, clause.Value) : $"position(lower({Add(FieldKind.Text, clause.Value)}) in lower({column})) > 0",
            CriteriaOp.NotContains => kind == FieldKind.Tags
                ? $"NOT {HasTag(column, clause.Value)}"
                : $"({column} IS NULL OR position(lower({Add(FieldKind.Text, clause.Value)}) in lower({column})) = 0)",
            _ => throw new InvalidOperationException($"Operator {clause.Op} is not a criteria operator."),
        };
    }

    private string HasTag(string column, JsonElement value)
        => $"EXISTS (SELECT 1 FROM unnest({column}) AS t(tag) WHERE lower(t.tag) = lower({Add(FieldKind.Text, value)}))";

    private string In(FieldKind kind, string column, JsonElement list)
    {
        var elements = list.EnumerateArray().ToList();
        var values = elements.Where(e => e.ValueKind != JsonValueKind.Null).Select(e => Convert(kind, e)).ToList();
        var parts = new List<string>();
        if (values.Count > 0)
        {
            parts.Add($"{column} = ANY({AddParameter(Array(kind, values))})");
        }
        if (values.Count < elements.Count)
        {
            parts.Add($"{column} IS NULL");
        }
        return string.Join(" OR ", parts);
    }

    private string Add(FieldKind kind, JsonElement value) => AddParameter(Convert(kind, value));

    private string AddParameter(object? value)
    {
        var name = $"{prefix}{_parameters.Count}";
        _parameters[name] = value;
        return "@" + name;
    }

    private object Convert(FieldKind kind, JsonElement value) => kind switch
    {
        FieldKind.Number => value.GetInt32(),
        FieldKind.Points => value.GetDecimal(),
        FieldKind.User or FieldKind.State or FieldKind.Sprint or FieldKind.WorkItem => value.GetString() == CriteriaClauseCheck.Me
            ? me ?? throw new InvalidOperationException("@me needs the current user.")
            : Guid.Parse(value.GetString()!),
        FieldKind.Date => DateTimeOffset.Parse(value.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).ToUniversalTime(),
        _ => value.GetString()!,
    };

    private static Array Array(FieldKind kind, List<object> values) => kind switch
    {
        FieldKind.Number => values.Cast<int>().ToArray(),
        FieldKind.Points => values.Cast<decimal>().ToArray(),
        FieldKind.User or FieldKind.State or FieldKind.Sprint or FieldKind.WorkItem => values.Cast<Guid>().ToArray(),
        _ => values.Cast<string>().ToArray(),
    };

    // The nearest Epic above the item, at any depth: null for an Epic itself and for an item with no Epic above it. UNION (not ALL)
    // stops the walk if the data ever holds a cycle.
    private string EpicOf()
        => $"(WITH RECURSIVE epic_walk AS (SELECT ew.id, ew.type, ew.parent_id FROM work_item ew WHERE ew.id = {alias}.parent_id "
        + "UNION SELECT ew.id, ew.type, ew.parent_id FROM work_item ew JOIN epic_walk ON ew.id = epic_walk.parent_id) SELECT epic_walk.id FROM epic_walk WHERE epic_walk.type = 'epic' LIMIT 1)";

    // The only place a field id becomes SQL text.
    private string Column(string field) => field switch
    {
        FieldIds.Id => $"{alias}.id",
        FieldIds.Key => $"{alias}.key",
        FieldIds.Title => $"{alias}.title",
        FieldIds.Type => $"{alias}.type",
        FieldIds.State => $"{alias}.state_id",
        FieldIds.StateCategory => $"(SELECT sc.category FROM state sc WHERE sc.id = {alias}.state_id)",
        FieldIds.Assignee => $"{alias}.assigned_to",
        FieldIds.Priority => $"{alias}.priority",
        FieldIds.Points => $"{alias}.story_points",
        FieldIds.Tags => $"{alias}.tags",
        FieldIds.Sprint => $"{alias}.sprint_id",
        FieldIds.Parent => $"{alias}.parent_id",
        FieldIds.Epic => EpicOf(),
        FieldIds.Severity => $"{alias}.severity",
        FieldIds.CreatedAt => $"{alias}.created_at",
        FieldIds.CreatedBy => $"{alias}.created_by",
        FieldIds.ChangedAt => $"{alias}.changed_at",
        FieldIds.ChangedBy => $"{alias}.changed_by",
        FieldIds.OriginalSystem => $"{alias}.original_system",
        FieldIds.OriginalSystemId => $"{alias}.original_system_id",
        _ => throw new InvalidOperationException($"{field} has no column."),
    };
}
