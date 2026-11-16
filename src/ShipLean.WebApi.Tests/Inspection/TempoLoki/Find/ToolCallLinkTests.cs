using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using ShipLean.Application.Features.Inspection;
using ShipLean.Application.Features.Inspection.TempoLoki;
using ShipLean.Contracts.Inspection;

namespace ShipLean.WebApi.Tests.Inspection.TempoLoki.Find;

// The FromToolCall link in the Tempo/Loki finder (K3, mirroring the ClickHouse rule): Loki gives candidates by a prefilter on the call's
// input labels, project-scoped and time-bounded, and LinkRules confirms each record, so a server named only inside tool_parameters counts.
[TestFixture(TestName = "Tempo and Loki tool-call links")]
public sealed class ToolCallLinkTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 9, 0, 0, TimeSpan.Zero);

    private static readonly InspectionOptions Options = new() { TempoUrl = "http://tempo.test", LokiUrl = "http://loki.test" };

    private static RunQuery Query(string item = "SHIP-1") => new("SHIP", item, Now.AddDays(-1), Now, null, null);

    private static string Stream(string labels)
        => "{\"data\":{\"result\":[{\"stream\":{\"service_name\":\"claude-code\",\"shiplean_project\":\"SHIP\",\"session_id\":\"s1\"," + labels + "},\"values\":[[\"" + StoreJson.StartNanos + "\",\"tool_result\"]]}]}}";

    private static string Label(string name, string value) => $"{JsonSerializer.Serialize(name)}:{JsonSerializer.Serialize(value)}";

    // The Loki labels of a Claude Code tool_result record: the server only inside tool_parameters, the arguments in tool_input.
    private static string ClaudeCodeCall(string server, string toolInput)
        => Label("tool_name", "mcp_tool") + "," + Label("tool_parameters", JsonSerializer.Serialize(new Dictionary<string, string> { ["mcp_server_name"] = server, ["mcp_tool_name"] = "get_brief" })) + "," + Label("tool_input", toolInput);

    private static (ScriptedStores Stores, TempoLokiRunFinder Finder) Build(string callLabels)
    {
        var time = new FakeTimeProvider(Now);
        var stores = new ScriptedStores((request, _) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/api/search", StringComparison.Ordinal))
            {
                return ScriptedStores.Json(StoreJson.Search([]));
            }
            var logQl = Uri.UnescapeDataString(request.RequestUri.Query);
            var candidates = logQl.Contains("tool_input=~", StringComparison.Ordinal) || logQl.Contains("session_id=\"s1\"", StringComparison.Ordinal);
            return ScriptedStores.Json(candidates ? Stream(callLabels) : StoreJson.EmptyLoki);
        });
        var clients = StoreClients.Create(Options, stores);
        return (stores, new TempoLokiRunFinder(new TempoRunFinder(clients, Options), new LokiRunFinder(clients, Options), new RunLinkCache(new InMemoryRunLinkStore(), time), Options, time));
    }

    [Test]
    public async Task Find_WhenTheServerIsOnlyInsideToolParameters_LinksTheItemFromTheToolCall()
    {
        var (_, finder) = Build(ClaudeCodeCall("shiplean", "{\"item\":\"SHIP-1\"}"));

        var found = await finder.FindAsync(Query(), CancellationToken.None);

        found.Runs.Count.ShouldBe(1);
        found.Runs[0].Links.ShouldBe([new RunItemLink("SHIP-1", LinkSource.FromToolCall)]);
    }

    [Test]
    public async Task Find_WhenTheToolParametersNameAnotherServer_DoesNotLink()
    {
        var (_, finder) = Build(ClaudeCodeCall("other", "{\"item\":\"SHIP-1\"}"));

        var found = await finder.FindAsync(Query(), CancellationToken.None);

        found.Runs.ShouldBeEmpty();
    }

    [Test]
    public async Task Find_WhenAnotherToolsParametersClaimTheShipleanServer_DoesNotLink()
    {
        var (_, finder) = Build(ClaudeCodeCall("shiplean", "{\"item\":\"SHIP-1\"}").Replace("\"mcp_tool\"", "\"Bash\"", StringComparison.Ordinal));

        var found = await finder.FindAsync(Query(), CancellationToken.None);

        found.Runs.ShouldBeEmpty();
    }

    [Test]
    public async Task Find_WhenTheItemIsEmpty_DoesNotLink()
    {
        var (_, finder) = Build(ClaudeCodeCall("shiplean", "{\"item\":\"\"}"));

        var found = await finder.FindAsync(Query(""), CancellationToken.None);

        found.Runs.ShouldBeEmpty();
    }

    [Test]
    public async Task Find_WhenTheInputNamesAnotherItemThanTheTopLevelOne_LinksOnlyTheTopLevelItem()
    {
        var (_, finder) = Build(ClaudeCodeCall("shiplean", "{\"item\":\"SHIP-2\",\"note\":\"SHIP-1\"}"));

        var found = await finder.FindAsync(Query(), CancellationToken.None);

        found.Runs.ShouldBeEmpty();
    }

    [Test]
    public async Task Find_Candidates_AreSearchedInTheProjectAndWindowWithTheItemEscaped()
    {
        var (stores, finder) = Build(ClaudeCodeCall("shiplean", "{\"item\":\"SHIP-1\"}"));

        const string item = "SHIP-1\"} or {a=~\".*";
        await finder.FindAsync(Query(item), CancellationToken.None);

        var called = stores.Requests.Select(r => Uri.UnescapeDataString(r.RequestUri!.Query)).First(q => q.Contains("tool_input=~", StringComparison.Ordinal));
        called.ShouldContain("shiplean_project=\"SHIP\"");
        called.ShouldContain("start=");
        called.ShouldContain("end=");
        called.ShouldContain("SHIP\\\\-1\\\\\\\"");
        called.ShouldNotContain("{a=~");
    }
}
