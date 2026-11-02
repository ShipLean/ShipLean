using ShipLean.Contracts.Retros;

namespace ShipLean.Application.Features.Retros.Reads;

// The totals of a set of points, worked out from their decisions (contract K2). Decidable is every point that is not Went well.
internal static class RetroAggregates
{
    public static RetroAggregateDto Of(IEnumerable<RetroPointDto> points)
    {
        var list = points.ToList();
        var decidable = list.Count(point => point.Decision is not null);
        var undecided = list.Count(point => point.Decision == RetroDecision.Undecided);
        return new RetroAggregateDto(
            new RetroCategoryCountsDto(
                list.Count(point => point.Category == RetroCategory.WentWell),
                list.Count(point => point.Category == RetroCategory.NotInPlan),
                list.Count(point => point.Category == RetroCategory.WentBadly),
                list.Count(point => point.Category == RetroCategory.Improve)),
            undecided,
            decidable - undecided,
            decidable,
            list.Select(point => point.Action?.WorkItemId).OfType<Guid>().Distinct().Count());
    }
}
