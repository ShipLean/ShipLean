using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Contracts.Inspection;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Inspection;

namespace ShipLean.WebApi.Tests.Inspection.Wiring;

// A host with Inspection on (Tempo/Loki store, never contacted) and a FakeTraceSource serving the claude-code fixture as a run of
// SHIP-124 in project SHIP. The project is off until a test turns it on through the settings route.
public abstract class InspectionApiFixture : ApiFixture
{
    protected const string Project = "SHIP";
    protected const string Item = "SHIP-124";
    protected const string OtherItem = "SHIP-90";

    private static readonly string[] Fixture = ["claude-code-synthetic"];

    protected FakeTraceSource Fake { get; } = new(InspectionFixtures.Synthetic.Where(f => Fixture.Contains(f.Name)));

    protected static string RunId => InspectionFixtures.Get(Fixture[0]).Expected.Summary.RunId;

    protected static string SettingsUrl => $"api/inspection/projects/{Project}/settings";

    protected static string RunsUrl(string item) => $"api/inspection/projects/{Project}/items/{item}/runs";

    protected override IReadOnlyDictionary<string, string?> HostSettings => new Dictionary<string, string?>
    {
        ["SHIPLEAN_INSPECTION"] = "on",
        ["SHIPLEAN_INSPECTION_STORE"] = "tempo-loki",
        ["SHIPLEAN_INSPECTION_TEMPO_URL"] = "http://127.0.0.1:9/tempo",
        ["SHIPLEAN_INSPECTION_LOKI_URL"] = "http://127.0.0.1:9/loki",
    };

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        Fake.Remap(Fixture[0], Project, Item, TimeProvider.System.GetUtcNow().AddMinutes(-1));
        services.RemoveAll<ITraceSource>();
        services.AddSingleton<ITraceSource>(Fake);
    };

    protected Task<HttpResponseMessage> TurnOnAsync(bool showContent = false)
        => Api.PutJsonAsync(SettingsUrl, new InspectionProjectSettingsDto(true, showContent, null, null, "tempoLoki"));
}
