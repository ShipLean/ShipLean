using ShipLean.Application.Plans;
using ShipLean.Contracts.Demos;
using ShipLean.Contracts.Views.Timeline;
using ShipLean.Core;

namespace ShipLean.Application.Features.Demos.Data;

// SHIP-2330: how long a Feature's plan took, taken from its timeline rather than worked out again. Waves come from the plan's
// stories (key and wave label, in key order): tagged waves in wave order, then each untagged story as a wave of its own. A wave's
// times are its stories' brackets on the timeline. Waits are the timeline's labelled edges (12 h or more), Blocked the Blocked
// periods of task bars. Every list is capped; Cut says whether a cap dropped anything.
internal static class DemoTiming
{
    internal const int MaxWaves = SprintDemoDataReader.MaxStoriesPerFeature;
    internal const int MaxStoryKeysPerWave = SprintDemoDataReader.MaxStoriesPerFeature;
    internal const int MaxLongest = 3;

    public static (SprintDemoTiming Timing, bool Cut) Build(FeatureTimelineViewDto timeline, IReadOnlyList<(string Key, WaveLabel? Wave)> planStories)
    {
        var stories = timeline.Stories.ToDictionary(story => story.Key, StringComparer.Ordinal);
        var groups = planStories.Where(story => story.Wave is not null)
            .GroupBy(story => story.Wave!)
            .OrderBy(group => group.Key)
            .Select(group => (Label: (string?)group.Key.Text, Keys: group.Select(story => story.Key).ToList()))
            .Concat(planStories.Where(story => story.Wave is null).Select(story => (Label: (string?)null, Keys: new List<string> { story.Key })))
            .ToList();
        var cut = groups.Count > MaxWaves || groups.Any(group => group.Keys.Count > MaxStoryKeysPerWave);
        var waves = groups.Take(MaxWaves).Select(group => Wave(group.Label, group.Keys, stories, timeline.AsOf)).ToList();

        var titles = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, title) in timeline.Stories.Select(story => (story.Key, story.Title)).Concat(timeline.Bars.Select(bar => (bar.Key, bar.Title))))
        {
            titles.TryAdd(key, title);
        }
        var waits = timeline.Edges
            .Where(edge => edge is { WaitHours: >= 12, WaitLabel: not null })
            .OrderByDescending(edge => edge.WaitHours).ThenBy(edge => edge.From, StringComparer.Ordinal).ThenBy(edge => edge.To, StringComparer.Ordinal)
            .Take(MaxLongest)
            .Select(edge => new SprintDemoWait(
                edge.From, titles.GetValueOrDefault(edge.From, string.Empty), edge.To, titles.GetValueOrDefault(edge.To, string.Empty),
                edge.Kind == TimelineEdgeKind.Blocked ? "blocked" : "depends", edge.WaitHours!.Value, edge.WaitLabel!, edge.OnCriticalPath))
            .ToList();
        var blocked = timeline.Bars
            .SelectMany(bar => bar.History.Where(period => period.State == TimelinePeriodState.Blocked && period.Hours is > 0)
                .Select(period => new SprintDemoBlocked(bar.Key, bar.Title, period.Hours!.Value, period.DurationLabel ?? DisplayDate.Duration(period.Hours.Value))))
            .OrderByDescending(period => period.Hours).ThenBy(period => period.TaskKey, StringComparer.Ordinal)
            .Take(MaxLongest)
            .ToList();

        var execution = new SprintDemoExecution(timeline.Execution.Start, timeline.Execution.End, timeline.Execution.CalendarDays);
        return (new SprintDemoTiming(execution, waves, waits, blocked, timeline.Links.Ui), cut);
    }

    private static SprintDemoWave Wave(string? label, List<string> keys, Dictionary<string, TimelineStoryDto> stories, DateTimeOffset asOf)
    {
        var brackets = keys.Select(key => stories.GetValueOrDefault(key)).ToList();
        var starts = brackets.Select(story => story?.Start).OfType<DateTimeOffset>().ToList();
        DateTimeOffset? start = starts.Count == 0 ? null : starts.Min();
        DateTimeOffset? end = brackets.All(story => story?.End is not null) ? brackets.Max(story => story!.End) : null;
        double? hours = start is { } from ? Math.Round(((end ?? asOf) - from).TotalHours, 1, MidpointRounding.AwayFromZero) : null;
        return new SprintDemoWave(label, [.. keys.Take(MaxStoryKeysPerWave)], start, end, hours, hours is { } h ? DisplayDate.Duration(h) : null);
    }
}
