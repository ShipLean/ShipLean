using System.Diagnostics;
using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using ShipLean.Contracts.Usage;
using ShipLean.TestSupport.Logging;
using Shouldly;

namespace ShipLean.TestSupport.Secrets;

// Asserts that no secret a test planted (an upload or transfer token, a signing secret, a callback token, a delivery body) reaches
// a surface it must never reach: a JSON response, an MCP result, a captured log line, a trace attribute or a usage event. Each
// secret is looked for as written, URL-escaped and form-encoded, since a URL carries it in any of those forms. A leak throws
// ShouldAssertException (not Assert.Fail, which marks the running test failed even when a test of this helper catches it).
public sealed class SecretLeakAssert
{
    // Writes strings unescaped, so a secret holding '+', '/' or '&' is found as it is, not as \u002B.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly string[] _forms;

    public SecretLeakAssert(params string[] secrets)
    {
        if (secrets.Length == 0 || secrets.Any(string.IsNullOrEmpty))
        {
            throw new ArgumentException("Give at least one secret, and no empty one: an empty string is in every text.", nameof(secrets));
        }

        _forms = [.. secrets.SelectMany(Forms).Distinct(StringComparer.Ordinal)];
    }

    public static IReadOnlyList<string> Forms(string secret) => [secret, Uri.EscapeDataString(secret), WebUtility.UrlEncode(secret)];

    // The span's name and every tag as "key=value", for NotInTraceAttributes. Call it from ActivityStopped: the Activity is recycled.
    public static IReadOnlyList<string> Attributes(Activity activity)
        => [activity.DisplayName, .. activity.TagObjects.Select(tag => $"{tag.Key}={tag.Value}"), .. activity.Baggage.Select(item => $"{item.Key}={item.Value}")];

    // A response body as sent: a serializer may escape a secret's characters (\u002B for '+'), so it is also read back unescaped.
    public void NotInJson(string json) => NotIn("the JSON body", [json, Unescaped(json)]);

    // An MCP tool result (the CallToolResult or its structured content), serialised as the client receives it.
    public void NotInMcpResult(object result) => NotIn("the MCP result", [JsonSerializer.Serialize(result, result.GetType(), Json)]);

    public void NotInLogs(RecordedLogs logs) => NotIn("a log line", logs.Events);

    public void NotInTraceAttributes(IEnumerable<string> attributes) => NotIn("a trace attribute", attributes);

    public void NotInUsageEvents(IEnumerable<UsageEvent> events) => NotIn("a usage event", events.Select(e => JsonSerializer.Serialize(e, Json)));

    private static string Unescaped(string json) => JsonNode.Parse(json)?.ToJsonString(Json) ?? string.Empty;

    private void NotIn(string surface, IEnumerable<string> texts)
    {
        foreach (var text in texts)
        {
            if (_forms.Any(form => text.Contains(form, StringComparison.Ordinal)))
            {
                throw new ShouldAssertException($"A secret reached {surface}: {Mask(text)}");
            }
        }
    }

    // The failing text with every form of every secret masked, so the failure message doesn't print one either.
    private string Mask(string text) => _forms.Aggregate(text, (masked, form) => masked.Replace(form, "<secret>", StringComparison.Ordinal));
}
