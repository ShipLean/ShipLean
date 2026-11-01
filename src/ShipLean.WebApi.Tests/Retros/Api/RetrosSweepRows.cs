using ShipLean.Contracts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.AccessSweep;

namespace ShipLean.WebApi.Tests.Retros.Api;

// The retro routes. The writes carry bodies the allowed leg answers with 400 or an empty success from the seams; nothing here deletes
// anything. The point and theme routes name the rows RetroSweepData inserts.
public sealed class RetrosSweepRows : IAccessSweepRows
{
    public IEnumerable<AccessSweepRow> Rows =>
    [
        new("GET", "api/sprints/{id:guid}/retro", ProjectRole.Member, Url: $"api/sprints/{SeedIds.Sprints.Sprint22}/retro"),
        new("GET", "api/projects/{id:guid}/retros", ProjectRole.Member),
        new("GET", "api/projects/{id:guid}/retros/insights", ProjectRole.Member),
        new("GET", "api/projects/{id:guid}/retros/themes", ProjectRole.Member),
        new("GET", "api/work-items/{id:guid}/retros", ProjectRole.Member, Url: $"api/work-items/{SeedIds.WorkItems.Ship90}/retros"),
        new("POST", "api/projects/{id:guid}/retros/points", ProjectRole.Member, Body: """{"category":"improve","text":""}"""),
        new("PUT", "api/retros/points/{id:guid}/theme", ProjectRole.Member, Body: """{"themeId":null,"themeName":null}""", Url: $"api/retros/points/{RetroSweepData.Point}/theme"),
        new("POST", "api/retros/points/{id:guid}/archive", ProjectRole.Member, Url: $"api/retros/points/{RetroSweepData.Point}/archive"),
        new("POST", "api/retros/points/{id:guid}/action", ProjectRole.Member, Body: """{"title":null,"points":null}""", Url: $"api/retros/points/{RetroSweepData.Point}/action"),
        new("PUT", "api/retros/themes/{id:guid}", ProjectRole.Member, Body: """{"name":"Sweep"}""", Url: $"api/retros/themes/{RetroSweepData.Theme}"),
        new("POST", "api/projects/{id:guid}/retros/themes/merge", ProjectRole.Member, Body: """{"sourceIds":[],"targetId":"0a2e0000-0000-7000-8000-000000000001"}"""),
    ];
}
