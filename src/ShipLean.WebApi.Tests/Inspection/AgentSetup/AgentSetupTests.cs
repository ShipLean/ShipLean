using ShipLean.WebApi.Tests.Taskboard;
using ShipLean.Contracts.Inspection;
using ShipLean.WebApi.Tests.Inspection.Wiring;

namespace ShipLean.WebApi.Tests.Inspection.AgentSetup;

// Every agent's files carry the project key, the collector endpoint, headers and the item, and never a store credential.
[TestFixture]
public class AgentSetupTests : InspectionApiFixture
{
    private const string Endpoint = "https://otel.example.test:4318";
    private const string StoreSecret = "default:store-secret-pw@ch.internal:9000";

    protected override IReadOnlyDictionary<string, string?> HostSettings => new Dictionary<string, string?>(base.HostSettings)
    {
        ["SHIPLEAN_INSPECTION_AGENT_OTLP_ENDPOINT"] = Endpoint,
        ["SHIPLEAN_INSPECTION_AGENT_OTLP_HEADERS"] = "x-team-key=collector-key",
        ["SHIPLEAN_INSPECTION_CLICKHOUSE_CONNECTION"] = StoreSecret,
        ["SHIPLEAN_INSPECTION_TEMPO_AUTH"] = "bearer:tempo-secret-token",
        ["SHIPLEAN_INSPECTION_LOKI_AUTH"] = "bearer:loki-secret-token",
    };

    private static string All(AgentSetupDto dto) => string.Join('\n', dto.Files.Select(f => f.Content).Concat(dto.Notes));

    [TestCase("claude-code")]
    [TestCase("codex-cli")]
    [TestCase("cursor")]
    public async Task Get_ForEachAgent_ShouldCarryProjectEndpointHeadersAndItemButNoStoreCredential(string agent)
    {
        var dto = await Api.GetAsync<AgentSetupDto>($"api/inspection/projects/{Project}/agent-setup?agent={agent}&item={Item}");
        var text = All(dto);

        dto.Agent.ShouldBe(agent);
        dto.DocsUrl.ShouldBe($"https://docs.shiplean.app/inspection/{agent}");
        dto.Files.ShouldNotBeEmpty();
        text.ShouldContain(Endpoint);
        text.ShouldContain("collector-key");
        text.ShouldContain($"project={Project}");
        text.ShouldContain(Item);
        text.ShouldNotContain("store-secret-pw");
        text.ShouldNotContain("tempo-secret-token");
        text.ShouldNotContain("loki-secret-token");
    }

    [Test]
    public async Task Get_ForCursor_ShouldServeTheHookScriptAndHooksJsonAsTheyAre()
    {
        var dto = await Api.GetAsync<AgentSetupDto>($"api/inspection/projects/{Project}/agent-setup?agent=cursor&item={Item}");

        dto.Files.Single(f => f.Name == ".cursor/hooks/shiplean-cursor-hook.mjs").Content.ShouldContain("buildLogRequest");
        dto.Files.Single(f => f.Name == ".cursor/hooks.json").Content.ShouldContain("afterFileEdit");
        dto.Files.Single(f => f.Name == ".shiplean").Content.ShouldContain($"item={Item}");
    }

    [Test]
    public async Task Get_ForTheOpenAiCli_ShouldWriteAnOtelTableWithTheHeadersAsToml()
    {
        var dto = await Api.GetAsync<AgentSetupDto>($"api/inspection/projects/{Project}/agent-setup?agent=codex-cli&item={Item}");

        var config = dto.Files.Single(f => f.Name == "config.toml").Content;
        config.ShouldContain("[otel]");
        config.ShouldContain($"endpoint = \"{Endpoint}/v1/logs\"");
        config.ShouldContain("headers = { \"x-team-key\" = \"collector-key\" }");
    }

    [Test]
    public async Task Get_WithoutAnItem_ShouldLeaveAPlaceholder()
    {
        var dto = await Api.GetAsync<AgentSetupDto>($"api/inspection/projects/{Project}/agent-setup?agent=claude-code");

        All(dto).ShouldContain("<item-key>");
    }
}
