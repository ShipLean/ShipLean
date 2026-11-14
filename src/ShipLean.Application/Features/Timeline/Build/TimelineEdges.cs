using ShipLean.Application.Features.Timeline.Build.Edges;
using ShipLean.Application.Features.Timeline.Facts;
using ShipLean.Application.Plans;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views.Timeline;

namespace ShipLean.Application.Features.Timeline.Build;

// Turns the facts' links and waves into arrows between the drawn bars, and finds the critical path once finished (D6, D7, D19). Pure.
public static class TimelineEdges
{
    // A predecessor that ended up to an hour after its successor started still counts as having released it.
    private static readonly TimeSpan Slack = TimeSpan.FromHours(1);

    public static TimelineEdgesPart Build(TimelineFacts facts, TimelineBarsPart bars, bool finished)
    {
        var boxes = EdgeBox.Index(bars);
        var edges = LinkEdges(facts, bars, boxes);
        edges.AddRange(WaveEdges(facts, bars, boxes, edges));

        var path = finished ? CriticalPath(bars, boxes, edges) : [];
        var onPath = path.ToHashSet(StringComparer.Ordinal);
        var marked = edges.Select(e => e with { OnCriticalPath = onPath.Contains(e.From) && onPath.Contains(e.To) }).ToList();
        return new TimelineEdgesPart(marked, path);
    }

    private static List<TimelineEdgeDto> LinkEdges(TimelineFacts facts, TimelineBarsPart bars, IReadOnlyDictionary<string, EdgeBox> boxes)
    {
        var made = new Dictionary<(string, string), TimelineEdgeDto>();
        var ordered = facts.Links
            .OrderBy(l => l.From, StringComparer.Ordinal)
            .ThenBy(l => l.To, StringComparer.Ordinal);
        foreach (var link in ordered)
        {
            if (!boxes.TryGetValue(link.From, out var from) || !boxes.TryGetValue(link.To, out var to))
            {
                continue;
            }

            var blocked = link.Kind == TimelineLinkKind.BlockedBy && to.Start is { } start && (from.End is not { } end || start < end);
            var edge = blocked ? Blocked(facts, bars, from, to) : Depends(from, to, TimelineEdgeSource.Link);
            // One arrow per pair; a blocked arrow wins over a plain one.
            if (!made.TryGetValue((link.From, link.To), out var existing) || (edge.Kind == TimelineEdgeKind.Blocked && existing.Kind != TimelineEdgeKind.Blocked))
            {
                made[(link.From, link.To)] = edge;
            }
        }
        return [.. made.Values];
    }

    private static TimelineEdgeDto Depends(EdgeBox from, EdgeBox to, TimelineEdgeSource source)
    {
        double? wait = from.End is { } end && to.Start is { } start ? WaitLabel.RoundOne((start - end).TotalHours) : null;
        return new TimelineEdgeDto(from.Key, to.Key, TimelineEdgeKind.Depends, source, wait, wait is { } w ? WaitLabel.For(w) : null, null, null, false);
    }

    private static TimelineEdgeDto Blocked(TimelineFacts facts, TimelineBarsPart bars, EdgeBox from, EdgeBox to)
    {
        // The moment the blocked item went back to work, once the blocker had ended.
        DateTimeOffset? unblocked = from.End is { } end
            ? facts.Moves
                .Where(m => m.ItemKey == to.Key && m.Category == StateCategory.InProgress && m.At >= end)
                .OrderBy(m => m.At).ThenBy(m => m.Rev)
                .Select(m => (DateTimeOffset?)m.At)
                .FirstOrDefault()
            : null;
        double? hours = unblocked is { } at ? WaitLabel.RoundOne((at - bars.Scale.Start).TotalHours) : null;
        return new TimelineEdgeDto(from.Key, to.Key, TimelineEdgeKind.Blocked, TimelineEdgeSource.Link, null, null, unblocked, hours, false);
    }

    // A task with no incoming link arrow follows the wave before its story's: the task that ended latest at or just after it started.
    private static List<TimelineEdgeDto> WaveEdges(
        TimelineFacts facts, TimelineBarsPart bars, IReadOnlyDictionary<string, EdgeBox> boxes, List<TimelineEdgeDto> linkEdges)
    {
        var waveOf = new Dictionary<string, WaveLabel>(StringComparer.Ordinal);
        foreach (var bar in bars.Bars)
        {
            if (facts.Waves.TryGetValue(bar.Parent, out var text) && WaveLabel.TryParse(text, out var label))
            {
                waveOf[bar.Key] = label;
            }
        }

        var joined = linkEdges.Select(e => e.To).ToHashSet(StringComparer.Ordinal);
        var earlier = waveOf
            .GroupBy(w => w.Value)
            .OrderByDescending(g => g.Key)
            .ToList();
        var edges = new List<TimelineEdgeDto>();
        foreach (var bar in bars.Bars)
        {
            if (!waveOf.TryGetValue(bar.Key, out var wave) || joined.Contains(bar.Key) || bar.Start is not { } start)
            {
                continue;
            }

            var source = earlier
                .Where(g => g.Key < wave)
                .Select(g => g.Select(w => boxes[w.Key]).Where(b => b.End is not null).ToList())
                .FirstOrDefault(tasks => tasks.Count > 0);
            var releaser = source?
                .Where(b => b.End <= start + Slack)
                .OrderByDescending(b => b.End).ThenBy(b => b.Key, StringComparer.Ordinal)
                .FirstOrDefault();
            if (releaser is not null)
            {
                edges.Add(Depends(releaser, boxes[bar.Key], TimelineEdgeSource.Wave));
            }
        }
        return edges;
    }

    private static List<string> CriticalPath(TimelineBarsPart bars, IReadOnlyDictionary<string, EdgeBox> boxes, List<TimelineEdgeDto> edges)
    {
        var last = bars.Bars
            .Where(b => b.End is not null && !b.Inferred)
            .OrderByDescending(b => b.End).ThenBy(b => b.Key, StringComparer.Ordinal)
            .FirstOrDefault();
        if (last is null)
        {
            return [];
        }

        var chain = new List<string>();
        var current = boxes[last.Key];
        while (current is not null && !chain.Contains(current.Key))
        {
            chain.Insert(0, current.Key);
            var at = current;
            current = at.Start is { } start
                ? edges
                    .Where(e => e.To == at.Key && e.Kind == TimelineEdgeKind.Depends)
                    .Select(e => boxes[e.From])
                    .Where(p => p.End is { } end && end <= start + Slack)
                    .OrderByDescending(p => p.End).ThenBy(p => p.Key, StringComparer.Ordinal)
                    .FirstOrDefault()
                : null;
        }

        var onChain = chain.ToHashSet(StringComparer.Ordinal);
        foreach (var edge in edges.Where(e => e.Kind == TimelineEdgeKind.Blocked && onChain.Contains(e.To) && !chain.Contains(e.From)))
        {
            chain.Add(edge.From);
        }
        return chain;
    }
}
