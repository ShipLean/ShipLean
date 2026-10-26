using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using ShipLean.Contracts;
using ShipLean.TestSupport.AccessSweep;

namespace ShipLean.WebApi.Tests.Access;

// The sweep's own rules, run over made-up routes and rows: each fails when the rule it names stops working.
[TestFixture]
public class StaleRowTests
{
    private static readonly ApiAction[] Mapped =
        [new("GET", "api/projects/{id:guid}", false), new("PUT", "api/projects/{id:guid}", false)];

    // The sweep reports a row whose method and template are mapped by no endpoint, so a renamed route can't keep a stale row.
    [Test]
    public void AccessSweep_WhenRowMatchesNoRoute_ShouldFail()
    {
        AccessSweepRow[] rows =
        [
            new("GET", "api/projects/{id:guid}", ProjectRole.Member),
            new("DELETE", "api/projects/{id:guid}", ProjectRole.Admin),
            new("GET", "api/project/{id:guid}", ProjectRole.Member),
        ];

        var stale = ApiActions.Unmatched(rows, Mapped);

        stale.Select(r => $"{r.Method} {r.RouteTemplate}").ShouldBe(["DELETE api/projects/{id:guid}", "GET api/project/{id:guid}"]);
    }

    [Test]
    public void Uncovered_WhenInstanceAdminOutsideProjects_ShouldBeCoveredByTheAttribute()
    {
        ApiAction[] actions = [new("GET", "api/instance/settings", true), new("GET", "api/instance/other", false)];

        var uncovered = ApiActions.Uncovered(actions, [], _ => false);

        uncovered.Select(a => a.Template).ShouldBe(["api/instance/other"]);
    }

    [Test]
    public void Uncovered_WhenInstanceAdminUnderProjects_ShouldStillNeedAProjectRow()
    {
        ApiAction[] actions = [new("GET", "api/projects/{id:guid}/danger", true)];
        AccessSweepRow instanceOnly = new("GET", "api/projects/{id:guid}/danger", ProjectRole.Member, InstanceScoped: true);
        AccessSweepRow project = new("GET", "api/projects/{id:guid}/danger", ProjectRole.Member);

        ApiActions.Uncovered(actions, [instanceOnly], _ => false).ShouldNotBeEmpty();
        ApiActions.Uncovered(actions, [project], _ => false).ShouldBeEmpty();
    }

    [Test]
    public void InstanceAdminWithoutRow_WhenNoInstanceScopedRow_ShouldReportTheRoute()
    {
        ApiAction[] actions = [new("GET", "api/instance/settings", true), new("GET", "api/instance/users", true)];
        AccessSweepRow[] rows =
        [
            new("GET", "api/instance/settings", ProjectRole.Member, InstanceScoped: true),
            new("GET", "api/instance/users", ProjectRole.Member),
        ];

        ApiActions.InstanceAdminWithoutRow(actions, rows).Select(a => a.Template).ShouldBe(["api/instance/users"]);
    }

    [Test]
    public void RowsNeedingUrl_WhenTemplateHasAnotherParameter_ShouldReportItUnlessUrlIsSet()
    {
        AccessSweepRow[] rows =
        [
            new("GET", "api/projects/{id:guid}/sprints", ProjectRole.Member),
            new("GET", "api/projects/{id:guid}/sprints/{sprintId:guid}", ProjectRole.Member),
            new("GET", "api/projects/{id:guid}/sprints/{sprintId:guid}/x", ProjectRole.Member, Url: "api/projects/p/sprints/{sprintId:guid}/x"),
            new("GET", "api/projects/by-key/{key}", ProjectRole.Member, Url: "api/projects/by-key/SHIP"),
            new("GET", "api/work-items/{itemId:guid}", ProjectRole.Member),
        ];

        ApiActions.RowsNeedingUrl(rows).Select(r => r.RouteTemplate).ShouldBe(
        [
            "api/projects/{id:guid}/sprints/{sprintId:guid}",
            "api/projects/{id:guid}/sprints/{sprintId:guid}/x",
            "api/work-items/{itemId:guid}",
        ]);
    }

    [Test]
    public void WithParameters_WhenEntryHasAParameter_ShouldAllowOnlyCallerOwnedRoutes()
    {
        RouteAllowList.Entry[] entries =
        [
            new("GET", "api/me/preferences/{key}", "x"),
            new("GET", "api/projects/{id}/export", "x"),
            new("GET", "api/things/{id}", "x"),
            new("GET", "api/me", "x"),
        ];

        RouteAllowList.WithParameters(entries).Select(e => e.Template).ShouldBe(["api/projects/{id}/export", "api/things/{id}"]);
    }

    [Test]
    public void MethodsOf_WhenEndpointDeclaresNoVerb_ShouldNotAssumeGet()
    {
        var endpoint = new RouteEndpoint(_ => Task.CompletedTask, RoutePatternFactory.Parse("api/x"), 0, new EndpointMetadataCollection(), "x");

        ApiActions.MethodsOf(endpoint).ShouldBe([ApiAction.NoVerb]);
    }
}
