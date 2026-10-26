using ShipLean.Contracts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.AccessSweep;

namespace ShipLean.WebApi.Tests.Sprints;

// The sprint write routes. Each sprint route points at a SHIP sprint no other row uses; the list route is the shell's.
// The rollover row targets a sprint that has not ended: the sweep only proves access, and the apply itself is the rollover API tests.
public sealed class SprintsRows : IAccessSweepRows
{
    public IEnumerable<AccessSweepRow> Rows =>
    [
        new("POST", "api/projects/{id:guid}/sprints", ProjectRole.Member, Body: """{"name":"Sweep sprint","startDate":"2090-01-01","endDate":"2090-01-14","goal":""}"""),
        new("PATCH", "api/sprints/{id:guid}", ProjectRole.Member, Body: "{}", Url: $"api/sprints/{SeedIds.Sprints.Sprint20}"),
        new("GET", "api/sprints/{id:guid}/capacity", ProjectRole.Member, Url: $"api/sprints/{SeedIds.Sprints.Sprint21}/capacity"),
        new("PUT", "api/sprints/{id:guid}/capacity", ProjectRole.Member, Body: """{"members":[]}""", Url: $"api/sprints/{SeedIds.Sprints.Sprint26}/capacity"),
        new("GET", "api/sprints/{id:guid}/rollover", ProjectRole.Member, Url: $"api/sprints/{SeedIds.Sprints.Sprint19}/rollover"),
        new("POST", "api/sprints/{id:guid}/rollover", ProjectRole.Member, Body: """{"moves":[]}""", Url: $"api/sprints/{SeedIds.Sprints.Sprint25}/rollover"),
        new("PUT", "api/projects/{id:guid}/forecast", ProjectRole.Member, Body: """{"velocityOverride":null}"""),
    ];
}
