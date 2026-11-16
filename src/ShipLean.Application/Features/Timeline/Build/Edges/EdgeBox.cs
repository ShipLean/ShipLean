using ShipLean.Application.Features.Timeline.Facts;

namespace ShipLean.Application.Features.Timeline.Build.Edges;

// The span an arrow attaches to: a task's bar, or a story's or bug's bracket (D19: only items of this Feature have one).
// Start is null for an inferred bar (D4) and for an item that never started; End is null while it is open.
internal sealed record EdgeBox(string Key, bool IsTask, bool Inferred, DateTimeOffset? Start, DateTimeOffset? End)
{
    public static IReadOnlyDictionary<string, EdgeBox> Index(TimelineBarsPart bars)
    {
        var boxes = new Dictionary<string, EdgeBox>(StringComparer.Ordinal);
        foreach (var story in bars.Stories)
        {
            boxes[story.Key] = new EdgeBox(story.Key, false, false, story.Start, story.End);
        }
        foreach (var bar in bars.Bars)
        {
            boxes[bar.Key] = new EdgeBox(bar.Key, true, bar.Inferred, bar.Start, bar.End);
        }
        return boxes;
    }
}
