using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ShipLean.Contracts.Import;
using ShipLean.Contracts.Import.Jira;

namespace ShipLean.Application.Features.Import.Jira;

// Jira's JSON to the contract's records, for Cloud (v3) and Server/Data Center (v2) alike.
public static partial class JiraReader
{
    // Server serves a story's epic in this custom field; Cloud serves it as the parent.
    private const string EpicLinkField = "customfield_10014";

    // A field Jira always sends; its absence means the site did not answer like Jira (a FormatException, which the client reports).
    public static string Required(JsonNode? node) => (string?)node ?? throw new FormatException("A field Jira always sends is missing.");

    public static JiraUser? User(JsonNode? node)
    {
        if (node is not JsonObject user)
        {
            return null;
        }
        var id = (string?)user["accountId"] ?? (string?)user["key"] ?? (string?)user["name"];
        var displayName = (string?)user["displayName"] ?? id;
        return id is null || displayName is null ? null : new JiraUser(id, displayName, (string?)user["emailAddress"], (string?)user["accountType"] == "app");
    }

    public static JiraComment Comment(JsonNode node)
        => new(Required(node["id"]), User(node["author"]), JiraAdfText.From(node["body"]) ?? string.Empty, Date((string?)node["created"]) ?? default, Date((string?)node["updated"]) ?? Date((string?)node["created"]) ?? default);

    public static JiraIssue Issue(JsonObject issue, IReadOnlyList<string> numberFieldIds)
    {
        var fields = issue["fields"]?.AsObject() ?? [];
        return new JiraIssue(
            Required(issue["key"]), Required(issue["id"]), Name(fields["issuetype"]) ?? string.Empty, Name(fields["status"]) ?? string.Empty,
            (string?)fields["summary"] ?? string.Empty, JiraAdfText.From(fields["description"]), User(fields["assignee"]), User(fields["reporter"]), Name(fields["priority"]),
            Numbers(fields, numberFieldIds), JiraSprintReader.Read(fields), (string?)fields["parent"]?["key"] ?? EpicLink(fields),
            Names(fields["labels"], null), Names(fields["components"], "name"), Date((string?)fields["created"]) ?? default, Date((string?)fields["updated"]) ?? default,
            Links(fields["issuelinks"]), Attachments(fields["attachment"]), Changelog(issue["changelog"]));
    }

    public static DateTimeOffset? Date(string? text)
        => text is not null && DateTimeOffset.TryParse(OffsetColon().Replace(text, "$1:$2"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date) ? date : null;

    private static string? Name(JsonNode? node) => (string?)node?["name"];

    private static string? EpicLink(JsonObject fields) => fields[EpicLinkField] is JsonValue value && value.TryGetValue<string>(out var key) ? key : null;

    private static IReadOnlyDictionary<string, decimal> Numbers(JsonObject fields, IReadOnlyList<string> numberFieldIds)
    {
        var numbers = new Dictionary<string, decimal>();
        foreach (var id in numberFieldIds.Distinct())
        {
            if (fields[id] is JsonValue value && value.TryGetValue<decimal>(out var number))
            {
                numbers[id] = number;
            }
        }
        return numbers;
    }

    private static List<string> Names(JsonNode? node, string? property)
        => node is JsonArray items ? [.. items.Select(i => property is null ? (string?)i : (string?)i?[property]).OfType<string>()] : [];

    private static List<JiraIssueLink> Links(JsonNode? node)
    {
        var links = new List<JiraIssueLink>();
        foreach (var link in (node as JsonArray ?? []).OfType<JsonObject>())
        {
            var type = (string?)link["type"]?["name"] ?? string.Empty;
            if ((string?)link["outwardIssue"]?["key"] is { } outward)
            {
                links.Add(new JiraIssueLink(type, JiraLinkDirection.Outward, outward));
            }
            else if ((string?)link["inwardIssue"]?["key"] is { } inward)
            {
                links.Add(new JiraIssueLink(type, JiraLinkDirection.Inward, inward));
            }
        }
        return links;
    }

    private static List<JiraAttachment> Attachments(JsonNode? node)
        => [.. (node as JsonArray ?? []).OfType<JsonObject>().Select(a => new JiraAttachment(
            Required(a["id"]), (string?)a["filename"] ?? string.Empty, (string?)a["mimeType"] ?? "application/octet-stream", (long?)a["size"] ?? 0, (string?)a["content"] ?? string.Empty))];

    private static List<JiraChangelogEntry> Changelog(JsonNode? node)
        => [.. (node?["histories"] as JsonArray ?? []).OfType<JsonObject>()
            .Select(h => new JiraChangelogEntry(
                Required(h["id"]), User(h["author"]), Date((string?)h["created"]) ?? default,
                [.. (h["items"] as JsonArray ?? []).OfType<JsonObject>().Select(i => new JiraChangeItem((string?)i["field"] ?? string.Empty, (string?)i["fromString"], (string?)i["toString"]))]))
            .OrderBy(e => e.Created)];

    // Jira writes offsets as +0700; DateTimeOffset wants +07:00.
    [GeneratedRegex(@"([+-]\d{2})(\d{2})$")]
    private static partial Regex OffsetColon();
}
