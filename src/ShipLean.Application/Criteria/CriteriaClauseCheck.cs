using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using ShipLean.Contracts.Criteria;
using ShipLean.Contracts.Persisted;

namespace ShipLean.Application.Criteria;

// The rules for one clause: the field is a criteria field, the operator is one it allows, the value has the field's type.
internal static partial class CriteriaClauseCheck
{
    public const string Me = "@me";

    public static CriteriaValidationError? Check(int index, CriteriaClause clause)
    {
        var definition = FieldIds.All.FirstOrDefault(f => f.Id == clause.Field && f.UsedIn.Contains(FieldUse.Criteria));
        if (definition is null)
        {
            return new CriteriaValidationError(index, clause.Field, "unknownField", $"{clause.Field} is not a field you can filter on.");
        }
        if (!definition.Ops.Contains(clause.Op))
        {
            return new CriteriaValidationError(index, clause.Field, "operatorNotAllowed", $"{definition.Label} does not allow {clause.Op}.");
        }
        return ValueOk(definition, clause.Op, clause.Value)
            ? null
            : new CriteriaValidationError(index, clause.Field, "invalidValue", $"The value is not valid for {definition.Label}.");
    }

    private static bool ValueOk(FieldDefinition definition, CriteriaOp op, JsonElement value) => op switch
    {
        CriteriaOp.IsEmpty or CriteriaOp.IsNotEmpty => value.ValueKind == JsonValueKind.Null,
        CriteriaOp.In => value.ValueKind == JsonValueKind.Array && value.GetArrayLength() > 0
            && value.EnumerateArray().All(e => e.ValueKind == JsonValueKind.Null ? definition.Nullable : ScalarOk(definition, e)),
        CriteriaOp.Contains or CriteriaOp.NotContains => value.ValueKind == JsonValueKind.String,
        CriteriaOp.GreaterThan or CriteriaOp.LessThan => ScalarOk(definition, value),
        _ => ScalarOk(definition, value),
    };

    // State categories and types are stored in camelCase and compare exactly; only text and tags ignore case.
    private static bool ScalarOk(FieldDefinition definition, JsonElement value) => definition.Kind switch
    {
        FieldKind.Number => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _),
        FieldKind.Points => PointsOk(value),
        FieldKind.User => value.ValueKind == JsonValueKind.String && (value.GetString() == Me || IdOk(value)),
        FieldKind.State or FieldKind.Sprint or FieldKind.WorkItem => IdOk(value),
        FieldKind.StateCategory or FieldKind.Type => value.ValueKind == JsonValueKind.String
            && (definition.Values ?? []).Any(v => v.Value.ValueKind == JsonValueKind.String && v.Value.GetString() == value.GetString()),
        FieldKind.Date => DateOk(value),
        _ => value.ValueKind == JsonValueKind.String,
    };

    private static bool IdOk(JsonElement value)
        => value.ValueKind == JsonValueKind.String && Guid.TryParseExact(value.GetString(), "D", out _);

    // story_points is numeric(6, 2): at most two decimals and four integer digits, so every valid value fits the column.
    private static bool PointsOk(JsonElement value)
        => value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var points)
        && decimal.Round(points, 2) == points && Math.Abs(points) <= MaxPoints;

    private const decimal MaxPoints = 9999.99m;

    // An ISO 8601 instant with its offset (Z or +hh:mm), so every evaluator reads it the same way.
    private static bool DateOk(JsonElement value)
        => value.ValueKind == JsonValueKind.String && value.GetString() is { } text && IsoInstant().IsMatch(text)
        && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d+)?)?(Z|[+-]\d{2}:\d{2})$")]
    private static partial Regex IsoInstant();
}
