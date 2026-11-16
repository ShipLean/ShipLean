using ShipLean.Application.Features.Timeline.Build;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views.Timeline;
using ShipLean.TestSupport.Timeline;

namespace ShipLean.UnitTest.Timeline;

// A hand-made TimelineBarsPart for the edges builder's tests: tasks and story brackets placed by hours from the scale start.
internal sealed class TimelineBarsPartBuilder
{
    public static readonly DateTimeOffset Zero = TimelineFactsBuilder.Start;

    private readonly List<TimelineBarDto> _bars = [];
    private readonly List<TimelineStoryDto> _stories = [];

    public TimelineBarsPartBuilder Task(string key, string parent, double? startHours, double? endHours, bool inferred = false)
    {
        _bars.Add(new TimelineBarDto(
            key, WorkItemTypeId.Task, key, parent, null, endHours is null ? TimelineBarState.InFlight : TimelineBarState.Resolved,
            At(startHours), At(endHours), startHours, endHours, null, null, null, null, inferred, false, [], [], null, null, null));
        return this;
    }

    public TimelineBarsPartBuilder Story(string key, double? startHours, double? endHours)
    {
        _stories.Add(new TimelineStoryDto(key, WorkItemTypeId.Story, key, null, At(startHours), At(endHours), startHours, endHours, null, null, null, null, []));
        return this;
    }

    public TimelineBarsPart Build(bool finished = true)
        => new(
            new TimelineFeatureDto(TimelineFactsBuilder.FeatureId, "SHIP-99", "Coupons v2", "Done", StateCategory.Completed, null),
            finished, [], [], new TimelineExecutionDto(null, null, null),
            new TimelineScaleDto(Zero, 400, null, [], []), _stories, _bars, []);

    private static DateTimeOffset? At(double? hours) => hours is { } h ? Zero.AddHours(h) : null;
}
