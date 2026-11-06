using Cronos;
using CronExpressionDescriptor;

namespace ShipLean.Application.Features.Backups.Cron;

// Cronos parses (standard 5 fields, UTC) and CronExpressionDescriptor describes. The gap rule walks every occurrence in
// the next 400 days (at most 20,000 of them), so an expression that runs too often only on some days, such as
// "0 0,30 1 * *", is refused too.
public sealed class CronSchedule(TimeProvider time) : ICronSchedule
{
    public const string WrongFieldsError = "Enter 5 fields: minute, hour, day of month, month and day of week.";
    public const string InvalidError = "Check each field: minute 0-59, hour 0-23, day of month 1-31, month 1-12 and day of week 0-6.";
    public const string TooOftenError = "Runs must be at least 1 hour apart.";
    public const string NeverError = "This schedule doesn't run in the next 400 days.";

    private static readonly TimeSpan MinimumGap = TimeSpan.FromHours(1);
    private static readonly TimeSpan Horizon = TimeSpan.FromDays(400);
    private const int MAX_OCCURRENCES = 20_000;

    private static readonly Options DescriptionOptions = new() { Use24HourTimeFormat = true, Locale = "en", ThrowExceptionOnParseError = false };

    public CronPreview Preview(string cron, DateTimeOffset from, int count = 5)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        var (expression, parseError) = Parse(cron);
        if (expression is null)
        {
            return new CronPreview(false, parseError, null, []);
        }
        var nextRuns = new List<DateTimeOffset>(count);
        var cursor = from.ToUniversalTime();
        while (nextRuns.Count < count && expression.GetNextOccurrence(cursor, TimeZoneInfo.Utc) is { } next)
        {
            nextRuns.Add(next);
            cursor = next;
        }
        var error = GapError(expression, from.ToUniversalTime());
        return new CronPreview(error is null, error, ExpressionDescriptor.GetDescription(cron.Trim(), DescriptionOptions), nextRuns);
    }

    public DateTimeOffset? Next(string cron, DateTimeOffset after)
        => Parse(cron).Expression?.GetNextOccurrence(after.ToUniversalTime(), TimeZoneInfo.Utc);

    public string? Validate(string cron)
    {
        var (expression, parseError) = Parse(cron);
        return expression is null ? parseError : GapError(expression, time.GetUtcNow());
    }

    private static (CronExpression? Expression, string? Error) Parse(string cron)
    {
        var fields = (cron ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length != 5)
        {
            return (null, WrongFieldsError);
        }
        return CronExpression.TryParse(string.Join(' ', fields), CronFormat.Standard, out var expression)
            ? (expression, null)
            : (null, InvalidError);
    }

    private static string? GapError(CronExpression expression, DateTimeOffset from)
    {
        DateTimeOffset? previous = null;
        var seen = 0;
        foreach (var occurrence in expression.GetOccurrences(from, from + Horizon, TimeZoneInfo.Utc, fromInclusive: false))
        {
            if (previous is { } last && occurrence - last < MinimumGap)
            {
                return TooOftenError;
            }
            previous = occurrence;
            if (++seen >= MAX_OCCURRENCES)
            {
                break;
            }
        }
        return previous is null ? NeverError : null;
    }
}
