using ShipLean.Contracts.Retros;

namespace ShipLean.Application.Features.Retros.Rules;

// The rules of the Insights views, pure so one set of tests pins them. Counts run oldest sprint first.
public static class RetroRules
{
    private const int RecurringWindow = 4;
    private const int RecurringMentions = 3;

    // wentBadly plus notInPlan points per sprint, one entry per sprint in the order given.
    public static IReadOnlyList<int> Mentions(IReadOnlyList<Guid> sprintIds, IEnumerable<(Guid SprintId, RetroCategory Category)> points)
    {
        var mentions = points.Where(p => p.Category is RetroCategory.WentBadly or RetroCategory.NotInPlan).ToList();
        return [.. sprintIds.Select(id => mentions.Count(p => p.SprintId == id))];
    }

    // Mentioned in at least 3 of the last 4 sprints, and no closed action item on the theme has the verdict helped.
    public static bool IsRecurring(IReadOnlyList<int> counts, bool hasHelpedFix)
        => !hasHelpedFix && counts.TakeLast(RecurringWindow).Count(count => count > 0) >= RecurringMentions;

    // Recurring first, then an open action item, then a helped fix, otherwise quiet.
    public static RetroThemeStatus Status(bool recurring, bool hasOpenAction, bool hasHelpedFix)
    {
        if (recurring)
        {
            return RetroThemeStatus.Recurring;
        }
        if (hasOpenAction)
        {
            return RetroThemeStatus.BeingAddressed;
        }
        return hasHelpedFix ? RetroThemeStatus.Resolved : RetroThemeStatus.Quiet;
    }

    // closedAt is the index of the sprint the action closed in; lastEndedIndex the index of the latest sprint that has ended.
    // Before is the average up to and including closedAt, after the average of the ended sprints since.
    public static RetroVerdictResult Verdict(IReadOnlyList<int> counts, int closedAt, int lastEndedIndex)
    {
        var before = counts.Take(closedAt + 1).DefaultIfEmpty(0).Average();
        var since = counts.Skip(closedAt + 1).Take(Math.Max(0, lastEndedIndex - closedAt)).ToList();
        if (since.Count == 0)
        {
            return new RetroVerdictResult(RetroVerdict.TooEarly, before, null, null);
        }

        var after = since.Average();
        var changePct = before == 0 ? (double?)null : (after - before) / before;
        if (after <= before / 2)
        {
            return new RetroVerdictResult(RetroVerdict.Helped, before, after, changePct);
        }
        return new RetroVerdictResult(after < before ? RetroVerdict.SomeEffect : RetroVerdict.NoEffect, before, after, changePct);
    }

    // A story, bug or action item finished in the sprint is done with a retro, otherwise missing. A feature waits until its last story-level child is completed.
    public static RetroCoverage Coverage(bool isFeature, bool lastChildCompleted, bool hasRetro)
    {
        if (isFeature && !lastChildCompleted)
        {
            return RetroCoverage.Waiting;
        }
        return hasRetro ? RetroCoverage.Done : RetroCoverage.Missing;
    }
}
