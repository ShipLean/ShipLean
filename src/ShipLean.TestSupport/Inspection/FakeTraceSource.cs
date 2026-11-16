using System.Text.Json.Nodes;
using ShipLean.Contracts.Inspection;

namespace ShipLean.TestSupport.Inspection;

// An ITraceSource over fixtures (K3), for tests that need no store. Remap points a fixture at a seeded project and item, and can
// move its times, so an acceptance test sees it under its own item and inside the item's search window.
public sealed class FakeTraceSource(IEnumerable<InspectionFixture> fixtures) : ITraceSource
{
    private const string ProjectAttribute = "shiplean.project";
    private const string ItemAttribute = "shiplean.item";

    private readonly Dictionary<string, (string ProjectKey, RawRun Run)> runs = fixtures.ToDictionary(f => f.Name, f => (ProjectOf(f.Expected), f.Expected));

    // Serves the fixture under the project key and, in place of its primary item (the one it is tagged with), the item key.
    // StartAt, when given, moves every time so the run starts then.
    public void Remap(string fixtureName, string projectKey, string itemKey, DateTimeOffset? startAt = null)
    {
        var (oldProject, run) = runs[fixtureName];
        var oldItem = PrimaryItem(run);
        var shift = startAt is { } start ? start - run.Summary.StartedAt : TimeSpan.Zero;
        runs[fixtureName] = (projectKey, Rewrite(run, oldProject, projectKey, oldItem, itemKey, shift));
    }

    public Task<RunSearch> FindRunsAsync(RunQuery query, CancellationToken ct)
    {
        var found = runs.Values
            .Where(r => r.ProjectKey == query.ProjectKey && Links(r.Run, query.ItemKey) && Overlaps(r.Run.Summary, query.From, query.To))
            .Select(r => r.Run.Summary)
            .OrderBy(s => s.StartedAt)
            .ToList();
        return Task.FromResult(new RunSearch(found, true, null));
    }

    public Task<RawRun?> GetRunAsync(RunRef run, CancellationToken ct)
    {
        var found = runs.Values
            .Where(r => r.ProjectKey == run.ProjectKey && r.Run.Summary.RunId == run.RunId && Links(r.Run, run.ItemKey) && Overlaps(r.Run.Summary, run.From, run.To))
            .Select(r => r.Run)
            .FirstOrDefault();
        return Task.FromResult(found);
    }

    public Task<StoreHealth> CheckAsync(CancellationToken ct) => Task.FromResult(new StoreHealth(true, 0, 0, null));

    public Task<IReadOnlyList<FieldCheck>> CheckFieldsAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<FieldCheck>>([new FieldCheck("item", ItemAttribute, true), new FieldCheck("project", ProjectAttribute, true)]);

    private static bool Links(RawRun run, string itemKey) => run.Summary.Links.Any(l => l.ItemKey == itemKey);

    private static bool Overlaps(RunSummary run, DateTimeOffset from, DateTimeOffset to) => run.StartedAt <= to && run.EndedAt >= from;

    private static string ProjectOf(RawRun run)
        => run.Spans.Select(s => s.Attributes).Concat(run.Logs.Select(l => l.Attributes))
            .Select(a => a.GetValueOrDefault(ProjectAttribute)).First(p => p is not null)!;

    private static string PrimaryItem(RawRun run)
        => (run.Summary.Links.FirstOrDefault(l => l.Source != LinkSource.FromToolCall) ?? run.Summary.Links[0]).ItemKey;

    // A ShipLean tool call links a run through the top-level item, key or task of its input (K3), so Remap moves those with the item.
    private static readonly string[] ToolInputAttributes = ["tool_parameters", "tool_input", "input"];

    private static readonly string[] ToolInputItemFields = ["item", "key", "task"];

    private static string RewriteToolInput(string json, string oldItem, string item)
    {
        if (!json.Contains(oldItem, StringComparison.Ordinal) || JsonNode.Parse(json) is not JsonObject input)
        {
            return json;
        }

        var changed = false;
        foreach (var field in ToolInputItemFields)
        {
            if (input[field] is JsonValue value && value.TryGetValue<string>(out var text) && text == oldItem)
            {
                input[field] = item;
                changed = true;
            }
        }
        return changed ? input.ToJsonString() : json;
    }

    private static RawRun Rewrite(RawRun run, string oldProject, string project, string oldItem, string item, TimeSpan shift)
    {
        string Value(string key, string value)
        {
            if (key == ProjectAttribute && value == oldProject)
            {
                return project;
            }
            if (key == ItemAttribute && value == oldItem)
            {
                return item;
            }
            return ToolInputAttributes.Contains(key) ? RewriteToolInput(value, oldItem, item) : value;
        }

        IReadOnlyDictionary<string, string> Attributes(IReadOnlyDictionary<string, string> source)
            => source.ToDictionary(p => p.Key, p => Value(p.Key, p.Value));

        var summary = run.Summary with
        {
            StartedAt = run.Summary.StartedAt + shift,
            EndedAt = run.Summary.EndedAt + shift,
            Links = [.. run.Summary.Links.Select(l => l.ItemKey == oldItem ? l with { ItemKey = item } : l)],
        };
        return run with
        {
            Summary = summary,
            Spans = [.. run.Spans.Select(s => s with
            {
                Start = s.Start + shift,
                End = s.End + shift,
                Attributes = Attributes(s.Attributes),
                Events = [.. s.Events.Select(e => e with { Time = e.Time + shift })],
            })],
            Logs = [.. run.Logs.Select(l => l with { Time = l.Time + shift, Attributes = Attributes(l.Attributes) })],
        };
    }
}
