using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.Contracts;
using ShipLean.Contracts.Inspection;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Inspection;

namespace ShipLean.AcceptanceTests.Inspection;

// Base of the Inspection screens' browser tests: an app host with SHIPLEAN_INSPECTION=on (Tempo/Loki store that is never contacted),
// a FakeTraceSource in place of the real one, and the seeded project SHIP's Inspection turned on through the settings PUT in setup,
// not in db/seed. Each fixture in FixtureNames is served as a run of ItemKey, starting a minute ago so it sits inside the item's window.
public abstract class InspectionAcceptanceTest : SeededAcceptanceTest
{
    protected const string ProjectKey = "SHIP";

    protected const string ItemKey = "SHIP-124";

    protected FakeTraceSource Fake { get; private set; } = null!;

    // The synthetic fixtures this class serves.
    protected virtual IReadOnlyList<string> FixtureNames => ["claude-code-synthetic"];

    // Show prompts and tool input/output on the Run page (D4).
    protected virtual bool ShowContent => false;

    protected override IReadOnlyDictionary<string, string?> HostSettings => new Dictionary<string, string?>
    {
        ["SHIPLEAN_INSPECTION"] = "on",
        ["SHIPLEAN_INSPECTION_STORE"] = "tempo-loki",
        ["SHIPLEAN_INSPECTION_TEMPO_URL"] = "http://127.0.0.1:9/tempo",
        ["SHIPLEAN_INSPECTION_LOKI_URL"] = "http://127.0.0.1:9/loki",
    };

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        Fake = new FakeTraceSource(InspectionFixtures.Synthetic.Where(f => FixtureNames.Contains(f.Name)));
        foreach (var name in FixtureNames)
        {
            Fake.Remap(name, ProjectKey, ItemKey, TimeProvider.System.GetUtcNow().AddMinutes(-1));
        }
        services.RemoveAll<ITraceSource>();
        services.AddSingleton<ITraceSource>(Fake);
    };

    [OneTimeSetUp]
    public async Task TurnOnInspectionForTheProjectAsync()
    {
        var joel = TestUser.Joel;
        using var http = new HttpClient(new HttpClientHandler { UseCookies = true }) { BaseAddress = new Uri(BaseUrl) };
        http.DefaultRequestHeaders.Add(ClientHeader.Name, "acceptance-test");
        using var login = await http.PostAsJsonAsync("/api/auth/login", new { email = joel.Email, password = joel.Password });
        login.EnsureSuccessStatusCode();
        using var put = await http.PutAsJsonAsync(
            $"/api/inspection/projects/{ProjectKey}/settings", new InspectionProjectSettingsDto(true, ShowContent, null, null, "tempoLoki"));
        put.EnsureSuccessStatusCode();
    }
}
