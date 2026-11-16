using ShipLean.Contracts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.AccessSweep;

namespace ShipLean.WebApi.Tests.InspectionMetrics;

// Inspection's lead-time read and its three usage reports (SHIP-1398 K3, K11). A member of SHIP gets in; anyone else gets not_found.
public sealed class InspectionMetricsAccessRows : IAccessSweepRows
{
    public IEnumerable<AccessSweepRow> Rows =>
    [
        new("GET", "api/projects/{projectId:guid}/inspection-metrics", ProjectRole.Member,
            Url: $"api/projects/{SeedIds.Projects.Ship}/inspection-metrics"),
        new("POST", "api/projects/{id:guid}/usage/inspection-metrics-viewed", ProjectRole.Member,
            Body: """{"view":"overview"}""", Url: $"api/projects/{SeedIds.Projects.Ship}/usage/inspection-metrics-viewed"),
        new("POST", "api/projects/{id:guid}/usage/inspection-metrics-period-opened", ProjectRole.Member,
            Body: """{"period":"finished"}""", Url: $"api/projects/{SeedIds.Projects.Ship}/usage/inspection-metrics-period-opened"),
        new("POST", "api/projects/{id:guid}/usage/inspection-metrics-task-opened", ProjectRole.Member,
            Body: """{"from":"rail"}""", Url: $"api/projects/{SeedIds.Projects.Ship}/usage/inspection-metrics-task-opened"),
    ];
}
