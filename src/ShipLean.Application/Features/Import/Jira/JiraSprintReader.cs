using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ShipLean.Contracts.Import.Jira;

namespace ShipLean.Application.Features.Import.Jira;

// Sprints on an issue. The Sprint field is a custom field whose id differs per site, so it is found by shape: Cloud serves
// objects ({id, name, state, startDate, ...}), Server/Data Center the greenhopper toString of each sprint.
public static partial class JiraSprintReader
{
    private const string GreenhopperMarker = "greenhopper.service.sprint.Sprint@";
    private const string NullText = "<null>";

    public static IReadOnlyList<JiraSprint> Read(JsonObject fields)
    {
        var sprints = new List<JiraSprint>();
        foreach (var (name, value) in fields)
        {
            if (!name.StartsWith("customfield_", StringComparison.Ordinal) || value is not JsonArray items)
            {
                continue;
            }
            sprints.AddRange(items.Select(Parse).OfType<JiraSprint>());
        }
        return [.. sprints.DistinctBy(s => s.Id)];
    }

    private static JiraSprint? Parse(JsonNode? item) => item switch
    {
        JsonObject element when element["state"] is JsonValue && element["name"] is JsonValue && element["id"] is JsonValue => FromObject(element),
        JsonValue text when text.TryGetValue<string>(out var value) && value.Contains(GreenhopperMarker, StringComparison.Ordinal) => FromGreenhopper(value),
        _ => null,
    };

    private static JiraSprint? FromObject(JsonObject element)
        => long.TryParse(element["id"]!.ToString(), CultureInfo.InvariantCulture, out var id) && IsState((string?)element["state"])
            ? new JiraSprint(id, (string)element["name"]!, ((string)element["state"]!).ToLowerInvariant(), JiraReader.Date((string?)element["startDate"]), JiraReader.Date((string?)element["endDate"]), Blank((string?)element["goal"]))
            : null;

    // "...Sprint@1a2b[id=111,rapidViewId=34,state=CLOSED,name=Platform Sprint 11,startDate=...,endDate=...,completeDate=<null>,sequence=111,goal=...]"; a name or goal may hold commas, so each value runs to the next known key.
    private static JiraSprint? FromGreenhopper(string text)
    {
        var parts = Keys().Matches(text);
        var values = new Dictionary<string, string>();
        for (var i = 0; i < parts.Count; i++)
        {
            var start = parts[i].Index + parts[i].Length;
            var end = i + 1 < parts.Count ? parts[i + 1].Index - 1 : text.LastIndexOf(']');
            values[parts[i].Groups[1].Value] = end >= start ? text[start..end] : string.Empty;
        }
        return values.TryGetValue("id", out var id) && long.TryParse(id, CultureInfo.InvariantCulture, out var number)
            && values.TryGetValue("name", out var name) && values.TryGetValue("state", out var state) && IsState(state)
            ? new JiraSprint(number, name, state.ToLowerInvariant(), Moment(values, "startDate"), Moment(values, "endDate"), Blank(values.GetValueOrDefault("goal")))
            : null;
    }

    private static DateTimeOffset? Moment(Dictionary<string, string> values, string key) => JiraReader.Date(Blank(values.GetValueOrDefault(key)));

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) || value == NullText ? null : value;

    private static bool IsState(string? state) => state is not null && (state.Equals("active", StringComparison.OrdinalIgnoreCase) || state.Equals("closed", StringComparison.OrdinalIgnoreCase) || state.Equals("future", StringComparison.OrdinalIgnoreCase));

    [GeneratedRegex(@"(?<=[\[,])(id|rapidViewId|state|name|startDate|endDate|completeDate|activatedDate|sequence|goal|autoStartStop|synced)=")]
    private static partial Regex Keys();
}
