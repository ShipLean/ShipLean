using ShipLean.Application.Features.InspectionMetrics.Periods;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Views.InspectionMetrics;

namespace ShipLean.Application.Features.InspectionMetrics;

// The query once validated (K3): the range and cut as enums (last6 and none when absent), the opened sprint, and the selected
// task's key. Parse throws validation on the field at fault: an unknown range or cut (names are the camelCase wire names), a period
// that is not one of the project's live sprints, or a select without a period.
public sealed record InspectionMetricsSelection(InspectionRange Range, InspectionCut Cut, InspectionSprint? Period, string? Select)
{
    public static InspectionMetricsSelection Parse(InspectionMetricsQuery query, IReadOnlyList<InspectionSprint> sprints)
    {
        var range = ParseEnum(query.Range, InspectionRange.Last6, "range", "Range must be last6, quarter, lastQuarter or all.");
        var cut = ParseEnum(query.Cut, InspectionCut.None, "cut", "Cut must be none, feature or who.");

        InspectionSprint? period = null;
        if (!string.IsNullOrEmpty(query.Period))
        {
            period = Guid.TryParse(query.Period, out var periodId) ? sprints.FirstOrDefault(sprint => sprint.Id == periodId) : null;
            if (period is null)
            {
                throw ApiException.Validation("Period must be one of the project's sprints.", "period");
            }
        }

        var select = string.IsNullOrEmpty(query.Select) ? null : query.Select;
        if (select is not null && period is null)
        {
            throw ApiException.Validation("Select needs a period.", "select");
        }

        return new InspectionMetricsSelection(range, cut, period, select);
    }

    private static T ParseEnum<T>(string? value, T absent, string field, string message)
        where T : struct, Enum
    {
        if (string.IsNullOrEmpty(value))
        {
            return absent;
        }
        foreach (var candidate in Enum.GetValues<T>())
        {
            if (string.Equals(WireName(candidate), value, StringComparison.Ordinal))
            {
                return candidate;
            }
        }
        throw ApiException.Validation(message, field);
    }

    private static string WireName<T>(T value)
        where T : struct, Enum
    {
        var name = value.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}
