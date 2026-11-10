using ShipLean.Contracts;
using ShipLean.TestSupport.AccessSweep;
using ShipLean.WebApi.Tests.Retros.Api;

namespace ShipLean.WebApi.Tests.Screens;

// The phone screens' routes (SHIP-916): the Features overview and digest by project, retro support by point. The support rows
// name the point RetroSweepData inserts.
public sealed class ScreensSweepRows : IAccessSweepRows
{
    public IEnumerable<AccessSweepRow> Rows =>
    [
        new("GET", "api/projects/{projectId:guid}/features/overview", ProjectRole.Member),
        new("GET", "api/projects/{projectId:guid}/features/digest", ProjectRole.Member),
        new("POST", "api/projects/{projectId:guid}/features/digest/seen", ProjectRole.Member),
        new("POST", "api/retros/points/{id:guid}/support", ProjectRole.Member, Url: $"api/retros/points/{RetroSweepData.Point}/support"),
        new("DELETE", "api/retros/points/{id:guid}/support", ProjectRole.Member, Url: $"api/retros/points/{RetroSweepData.Point}/support"),
    ];
}
