using System.Text;
using System.Text.Json;
using ShipLean.Contracts.Demos;

namespace ShipLean.Application.Features.Demos.Data;

// K7's size rule over the whole sprint: the compact JSON of the data stays within MaxBytes (the instructions carry it twice and stay under 300 KB).
// Every Feature stays, because the video has one chapter per Feature. Over the budget, each Feature is trimmed in levels, least useful
// first: its stories' tasks, their criteria, their summaries, its plan's wave timing (waves, waits, Blocked periods), its retro,
// attachments and pull requests, then its stories (StoriesOmitted says how many), and last Why and Measure. All start fully trimmed (the
// header: key, title, state, url, goal and plan facts with only the execution of their timing); then level by level, in key order, each
// Feature is given back one level while the total fits. A Feature that doesn't fit stays where it is and the rest carry on (SHIP-2330:
// one big Feature no longer leaves every later one without its stories). Then OtherWork and the sprint retro, each whole or empty, only
// when every Feature is whole. Truncated is set whenever the budget cuts anything.
public static class SprintDemoDataBudget
{
    public const int MaxBytes = 120 * 1024;

    // Trim(feature, level) applies the first `level` cuts; TrimLevels is the header.
    internal const int TrimLevels = 7;

    private static readonly SprintDemoRetroData NoRetro = new([], [], [], []);

    public static SprintDemoData Apply(SprintDemoData data, int maxBytes = MaxBytes)
    {
        if (Size(data) <= maxBytes)
        {
            return data;
        }

        var original = data.Features;
        var features = original.Select(feature => Trim(feature, TrimLevels)).ToList();
        var sizes = features.Select(Size).ToArray();
        var shrunk = data with { Features = features, OtherWork = [], SprintRetro = NoRetro, Truncated = true };
        var size = Size(shrunk);
        var stuck = new bool[features.Count];
        for (var level = TrimLevels - 1; level >= 0; level--)
        {
            for (var i = 0; i < features.Count; i++)
            {
                if (stuck[i])
                {
                    continue;
                }
                var next = Trim(original[i], level);
                var nextSize = Size(next);
                if (size + nextSize - sizes[i] <= maxBytes)
                {
                    size += nextSize - sizes[i];
                    features[i] = next;
                    sizes[i] = nextSize;
                }
                else
                {
                    stuck[i] = true;
                }
            }
        }

        var whole = !stuck.Any(isStuck => isStuck);
        var otherWork = data.OtherWork;
        var retro = data.SprintRetro;
        var otherSize = Size(otherWork) - Size(shrunk.OtherWork);
        var retroSize = Size(retro) - Size(NoRetro);
        var withOther = whole && size + otherSize <= maxBytes;
        size += withOther ? otherSize : 0;
        var withRetro = withOther && size + retroSize <= maxBytes;
        return shrunk with
        {
            Features = features,
            OtherWork = withOther ? otherWork : [],
            SprintRetro = withRetro ? retro : NoRetro,
        };
    }

    public static int Size<T>(T value) => Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(value));

    private static SprintDemoFeatureData Trim(SprintDemoFeatureData feature, int level)
    {
        var stories = level >= 6
            ? []
            : feature.Stories.Select(story => story with
            {
                Tasks = level >= 1 ? [] : story.Tasks,
                Criteria = level >= 2 ? [] : story.Criteria,
                Summary = level >= 3 ? null : story.Summary,
            }).ToList();
        var plan = level >= 4 && feature.Plan is { Timing: { } timing } facts
            ? facts with { Timing = timing with { Waves = [], LongestWaits = [], LongestBlocked = [] } }
            : feature.Plan;
        return feature with
        {
            Stories = stories,
            StoriesOmitted = feature.StoriesOmitted + (level >= 6 ? feature.Stories.Count : 0),
            Plan = plan,
            Retro = level >= 5 ? NoRetro : feature.Retro,
            AttachmentNames = level >= 5 ? [] : feature.AttachmentNames,
            PullRequests = level >= 5 ? [] : feature.PullRequests,
            Why = level >= 7 ? null : feature.Why,
            Measure = level >= 7 ? null : feature.Measure,
        };
    }
}
