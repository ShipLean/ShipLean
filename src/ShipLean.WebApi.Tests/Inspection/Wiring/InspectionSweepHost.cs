using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using ShipLean.Contracts.Inspection;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Inspection;

namespace ShipLean.WebApi.Tests.Inspection.Wiring;

// What the access sweeps need so the Inspection rows prove access and not the off switch: the instance on (a Tempo/Loki store that is
// never contacted), SHIP's Inspection on, and a FakeTraceSource serving one run of SHIP-124. Only the sweeps' hosts get it; every
// other route is swept as before.
public static class InspectionSweepHost
{
    public const string Fixture = "claude-code-synthetic";

    public const string Item = "SHIP-124";

    public static IReadOnlyDictionary<string, string?> Settings { get; } = new Dictionary<string, string?>
    {
        ["SHIPLEAN_INSPECTION"] = "on",
        ["SHIPLEAN_INSPECTION_STORE"] = "tempo-loki",
        ["SHIPLEAN_INSPECTION_TEMPO_URL"] = "http://127.0.0.1:9/tempo",
        ["SHIPLEAN_INSPECTION_LOKI_URL"] = "http://127.0.0.1:9/loki",
    };

    public static RawRun Run => InspectionFixtures.Get(Fixture).Expected;

    public static void UseFakeSource(IServiceCollection services)
    {
        var fake = new FakeTraceSource(InspectionFixtures.Synthetic.Where(f => f.Name == Fixture));
        fake.Remap(Fixture, "SHIP", Item, TimeProvider.System.GetUtcNow().AddMinutes(-1));
        services.RemoveAll<ITraceSource>();
        services.AddSingleton<ITraceSource>(fake);
    }

    // The runs routes answer not_found while the project is off, and the allowed leg refuses a 404.
    public static Task TurnOnShipAsync(NpgsqlConnection connection)
        => connection.ExecuteAsync(
            "INSERT INTO inspection_project_settings (project_id, enabled) VALUES (@project, true) ON CONFLICT (project_id) DO UPDATE SET enabled = true",
            new { project = SeedIds.Projects.Ship });
}
