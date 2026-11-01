using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.ProjectSettings.Api;

namespace ShipLean.WebApi.Tests.Usage;

// What the IProjectAccess decorator leaves in the request's usage scope.
[TestFixture]
public class UsageScopeTests : ApiFixture
{
    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
        services.AddControllers().AddApplicationPart(typeof(UsageProbeController).Assembly);

    private async Task<ArrangedProject> ArrangeAsync(ProjectRole? joel)
    {
        await using var connection = await OpenDatabaseAsync();
        return joel is { } role
            ? await ArrangedProject.CreateAsync(connection, (SeedIds.Users.Joel, role))
            : await ArrangedProject.CreateAsync(connection, (SeedIds.Users.Priya, ProjectRole.Member));
    }

    private Task<UsageProbeResult> ProbeAsync(params ArrangedProject[] projects)
        => Api.GetJsonAsync<UsageProbeResult>("api/test-usage/touch?" + string.Join('&', projects.Select(p => $"project={p.Id}")));

    [Test]
    public async Task ProjectScopedRequest_WhenAccessChecked_ShouldStampOneProject()
    {
        var project = await ArrangeAsync(ProjectRole.Member);

        var result = await ProbeAsync(project);

        result.ProjectCount.ShouldBe(1);
        result.ProjectId.ShouldBe(project.Id);
    }

    [Test]
    public async Task RequestTouchingTwoProjects_WhenAccessChecked_ShouldCountTwoAndLeaveIdNull()
    {
        var first = await ArrangeAsync(ProjectRole.Member);
        var second = await ArrangeAsync(ProjectRole.Admin);

        var result = await ProbeAsync(first, second, first);

        result.ProjectCount.ShouldBe(2);
        result.ProjectId.ShouldBeNull();
    }

    [Test]
    public async Task AccessRefused_WhenNotMember_ShouldNotStamp()
    {
        var project = await ArrangeAsync(null);

        var result = await ProbeAsync(project);

        result.ProjectCount.ShouldBe(0);
        result.ProjectId.ShouldBeNull();
    }
}
