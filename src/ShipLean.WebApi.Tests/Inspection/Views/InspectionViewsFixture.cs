using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Contracts.Inspection;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Inspection;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Inspection.Views;

// Inspection on, SHIP on (content hidden until a test says otherwise), and the claude-code fixture served as a run of SHIP-124 starting a minute
// ago. REST and MCP both run against it. One scenario per class, like every ApiFixture.
public abstract class InspectionViewsFixture : McpFixture
{
    protected const string Project = "SHIP";
    protected const string Item = "SHIP-124";
    protected const string Fixture = "claude-code-synthetic";

    // Text the fixture's run holds as content (prompts, tool output, tool input) and that a project hiding content must never send.
    protected static readonly string[] ContentTexts =
    [
        "Implement the thing in SHIP-1", "Now run the tests", "edited Foo.cs", "error CS1002", "picked up",
        """{"command":"dotnet build"}""", """{"file_path":"src/App/Foo.cs"}""", """{"file_path":"src/App/Bar.cs"}""",
    ];

    protected override TestUser SignedInAs => TestUser.Joel;

    protected override bool WithProbeTools => false;

    protected FakeTraceSource Fake { get; } = new(InspectionFixtures.Synthetic.Where(f => f.Name == Fixture));

    protected RecordingTraceSource Source { get; }

    protected InspectionViewsFixture() => Source = new RecordingTraceSource(Fake);

    protected static string RunId => InspectionFixtures.Get(Fixture).Expected.Summary.RunId;

    protected static string RunsUrl => $"api/inspection/projects/{Project}/items/{Item}/runs";

    protected override IReadOnlyDictionary<string, string?> HostSettings => new Dictionary<string, string?>
    {
        ["SHIPLEAN_INSPECTION"] = "on",
        ["SHIPLEAN_INSPECTION_STORE"] = "tempo-loki",
        ["SHIPLEAN_INSPECTION_TEMPO_URL"] = "http://127.0.0.1:9/tempo",
        ["SHIPLEAN_INSPECTION_LOKI_URL"] = "http://127.0.0.1:9/loki",
        ["SHIPLEAN_INSPECTION_GRAFANA_URL_TEMPLATE"] = "https://grafana.example.com/explore?traceId={traceId}",
    };

    protected override Action<IServiceCollection>? ConfigureTestServices
    {
        get
        {
            var baseConfigure = base.ConfigureTestServices;
            return services =>
            {
                baseConfigure?.Invoke(services);
                Fake.Remap(Fixture, Project, Item, TimeProvider.System.GetUtcNow().AddMinutes(-1));
                services.RemoveAll<ITraceSource>();
                services.AddSingleton<ITraceSource>(Source);
            };
        }
    }

    [SetUp]
    public async Task TurnOnInspectionForShip()
    {
        using var response = await SetContentAsync(false);

        response.EnsureSuccessStatusCode();
    }

    protected Task<HttpResponseMessage> SetContentAsync(bool showContent)
        => Api.PutJsonAsync($"api/inspection/projects/{Project}/settings", new InspectionProjectSettingsDto(true, showContent, null, null, "tempoLoki"));

    protected async Task<string> ToolTokenAsync() => await TokenAsync(TestUser.Joel, ReadOnly);

    protected static IEnumerable<string> Strings(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                yield return element.GetString()!;
                break;
            case JsonValueKind.Object:
                foreach (var text in element.EnumerateObject().SelectMany(p => Strings(p.Value)))
                {
                    yield return text;
                }
                break;
            case JsonValueKind.Array:
                foreach (var text in element.EnumerateArray().SelectMany(Strings))
                {
                    yield return text;
                }
                break;
            default:
                break;
        }
    }
}
