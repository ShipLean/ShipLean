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
        new("POST", "api/projects/{id:guid}/retros/actions", ProjectRole.Member, Body: """{"title":"","fixType":"skill","pointIds":[]}"""),
        new("POST", "api/retros/actions/{workItemId:guid}/points", ProjectRole.Member, Body: """{"pointIds":[]}""", Url: $"api/retros/actions/{SeedIds.WorkItems.Ship90}/points"),
        new("PUT", "api/retros/actions/{workItemId:guid}", ProjectRole.Member, Body: """{"fixType":"skill","themeId":"0a2e0000-0000-7000-8000-000000000001","themeName":"x"}""", Url: $"api/retros/actions/{SeedIds.WorkItems.Ship90}"),
        new("POST", "api/retros/points/{id:guid}/not-needed", ProjectRole.Member, Body: """{"reason":""}""", Url: $"api/retros/points/{RetroSweepData.Point}/not-needed"),
        new("POST", "api/retros/points/{id:guid}/undo-not-needed", ProjectRole.Member, Url: $"api/retros/points/{RetroSweepData.Point}/undo-not-needed"),
        new("PUT", "api/retros/themes/{id:guid}", ProjectRole.Member, Body: """{"name":"Sweep"}""", Url: $"api/retros/themes/{RetroSweepData.Theme}"),
        new("GET", "api/projects/{id:guid}/retros/features", ProjectRole.Member),
        new("GET", "api/projects/{id:guid}/retros/items/{key}", ProjectRole.Member, Url: $"api/projects/{SeedIds.Projects.Ship}/retros/items/SHIP-90"),
        new("GET", "api/projects/{id:guid}/retros/action-dialog", ProjectRole.Member),
        new("GET", "api/retros/actions/{workItemId:guid}", ProjectRole.Member, Url: $"api/retros/actions/{RetroSweepData.Action}"),
        new("POST", "api/retros/theme-merge-proposals/{id:guid}/merge", ProjectRole.Member, Url: $"api/retros/theme-merge-proposals/{RetroSweepData.Proposal}/merge"),
        new("POST", "api/retros/theme-merge-proposals/{id:guid}/keep-apart", ProjectRole.Member, Url: $"api/retros/theme-merge-proposals/{RetroSweepData.Proposal}/keep-apart"),
        new("POST", "api/projects/{id:guid}/retros/themes/merge", ProjectRole.Member, Body: """{"sourceIds":[],"targetId":"0a2e0000-0000-7000-8000-000000000001"}"""),
    ];
}
