using ShipLean.Contracts.Refs;

namespace ShipLean.Application.Features.Search.Ranking.Fusion;

// Boosts multiply the RRF score. They are factors, not additions, so they never turn a strong text match into a weak
// one: a rank-0 hit beats a fully boosted rank-99 hit in one list. They do reorder the top of the list, though. At
// k = 60 the open boost alone lifts rank 9 over a closed rank 0, and every boost together (about 1.46) lifts rank ~28
// over rank 0, so they reorder roughly the top 30 ranks. B6b tunes the factors, and should check that reach.
public static class SearchBoosts
{
    // Any category but Completed counts as open.
    public const double Open = 1.15;

    public const double AssignedToMe = 1.10;

    public const double RecentProject = 1.05;

    public const double RecentlyChanged = 1.10;

    public static readonly TimeSpan RecentWindow = TimeSpan.FromDays(7);

    // The score an exact key match gets: above any fused and boosted score.
    public const double ExactKeyScore = 1.0;

    public static double Factor(ItemFacts facts, Guid me, Guid? recentProject, DateTimeOffset now)
    {
        var factor = facts.Category == StateCategory.Completed ? 1.0 : Open;
        if (facts.AssigneeId == me)
        {
            factor *= AssignedToMe;
        }

        if (recentProject == facts.ProjectId)
        {
            factor *= RecentProject;
        }

        if (now - facts.ChangedAt <= RecentWindow)
        {
            factor *= RecentlyChanged;
        }

        return factor;
    }
}
