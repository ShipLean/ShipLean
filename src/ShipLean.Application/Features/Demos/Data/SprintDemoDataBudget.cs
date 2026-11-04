using System.Text;
using System.Text.Json;
using ShipLean.Contracts.Demos;

namespace ShipLean.Application.Features.Demos.Data;

// K7's size rule over the whole sprint: the compact JSON of the data stays within MaxBytes (the instructions carry it twice and stay under 300 KB).
// Every Feature stays, because the video has one chapter per Feature. All start as a header (key, title, state, url, goal, plan facts); in key
// order each is given back its stories, retro, attachments, Why and Measure while the total fits, and the first that doesn't fit ends the
// giving-back. Then OtherWork and the sprint retro, each whole or empty. Anything left as a header or empty sets Truncated.
public static class SprintDemoDataBudget
{
    public const int MaxBytes = 120 * 1024;

    private static readonly SprintDemoRetroData NoRetro = new([], [], [], []);

    public static SprintDemoData Apply(SprintDemoData data, int maxBytes = MaxBytes)
    {
        if (Size(data) <= maxBytes)
        {
            return data;
        }

        var shrunk = data with
        {
            Features = [.. data.Features.Select(Header)],
            OtherWork = [],
            SprintRetro = NoRetro,
            Truncated = true,
        };
        var size = Size(shrunk);
        var features = shrunk.Features.ToList();
        var giving = true;
        for (var i = 0; i < features.Count && giving; i++)
        {
            var extra = Size(data.Features[i]) - Size(features[i]);
            giving = size + extra <= maxBytes;
            if (giving)
            {
                features[i] = data.Features[i];
                size += extra;
            }
        }

        var otherWork = data.OtherWork;
        var retro = data.SprintRetro;
        var otherSize = Size(otherWork) - Size(shrunk.OtherWork);
        var retroSize = Size(retro) - Size(NoRetro);
        var withOther = giving && size + otherSize <= maxBytes;
        size += withOther ? otherSize : 0;
        var withRetro = giving && withOther && size + retroSize <= maxBytes;
        return shrunk with
        {
            Features = features,
            OtherWork = withOther ? otherWork : [],
            SprintRetro = withRetro ? retro : NoRetro,
        };
    }

    public static int Size<T>(T value) => Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(value));

    private static SprintDemoFeatureData Header(SprintDemoFeatureData feature)
        => feature with { Why = null, Measure = null, Stories = [], Retro = NoRetro, AttachmentNames = [], PullRequests = [] };
}
