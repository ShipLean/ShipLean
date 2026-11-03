using System.Net;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Retros;
using ShipLean.WebApi.Tests.ProjectSettings.Api;
using ShipLean.WebApi.Tests.Retros.People;

namespace ShipLean.WebApi.Tests.Retros.Merge;

// A person answers a merge proposal an agent made. Every test arranges a project of its own, and proposes through the same
// IRetroDecisionWrites.ProposeMergeAsync that the decide tool calls, so what the tests answer is what an agent would have left.
public sealed class RetroMergeProposalsTests : ApiFixture
{
    private static readonly RetroActor Agent = new(SeedIds.Users.Joel, "claude-x");

    [Test]
    public async Task Merge_ShouldMoveThePointsAndMarkMerged()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var (from, into) = (await world.Seed.ThemeAsync("CI screenshots"), await world.Seed.ThemeAsync("Screenshot baselines"));
        var retro = await world.AgentRetroAsync();
        var moving = new[]
        {
            await world.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "One.", ThemeId: from)),
            await world.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "Two.", ThemeId: from)),
        };
        await world.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "Archived.", ThemeId: from, Archived: true));
        await world.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "Stays.", ThemeId: into));
        await ProposeAsync(world, "CI screenshots", "Screenshot baselines");
        var pending = (await PendingAsync(world.Project.Id)).Single();
        pending.PointCount.ShouldBe(2);
        pending.From.Id.ShouldBe(from);
        pending.Into.Id.ShouldBe(into);

        using var response = await Api.PostJsonAsync($"/api/retros/theme-merge-proposals/{pending.Id}/merge");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await world.Connection.QueryAsync<Guid?>("SELECT theme_id FROM retro_point WHERE id = ANY(@moving)", new { moving })).ShouldBe([into, into]);
        (await world.Connection.QuerySingleAsync<Guid?>("SELECT merged_into_id FROM retro_theme WHERE id = @from", new { from })).ShouldBe(into);
        var stored = await world.Connection.QuerySingleAsync<(string Status, Guid? By, DateTime? At)>("SELECT status, decided_by_user_id AS By, decided_at AS At FROM retro_theme_merge_proposal WHERE id = @id", new { id = pending.Id });
        (stored.Status, stored.By).ShouldBe(("merged", SeedIds.Users.Joel));
        stored.At.ShouldNotBeNull();
        (await PendingAsync(world.Project.Id)).ShouldBeEmpty();
    }

    [Test]
    public async Task Merge_WhenAlreadyDecided_ShouldConflict()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        await world.Seed.ThemeAsync("Alpha");
        await world.Seed.ThemeAsync("Beta");
        await world.Seed.ThemeAsync("Gamma");
        await world.Seed.ThemeAsync("Delta");
        await ProposeAsync(world, "Alpha", "Beta");
        await ProposeAsync(world, "Gamma", "Delta");
        var (merged, kept) = (await ProposalIdAsync(world, "Alpha"), await ProposalIdAsync(world, "Gamma"));
        (await Api.PostJsonAsync($"/api/retros/theme-merge-proposals/{merged}/merge")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Api.PostJsonAsync($"/api/retros/theme-merge-proposals/{kept}/keep-apart")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        foreach (var route in new[] { $"{merged}/merge", $"{merged}/keep-apart", $"{kept}/merge", $"{kept}/keep-apart" })
        {
            using var response = await Api.PostJsonAsync($"/api/retros/theme-merge-proposals/{route}");
            (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Conflict)).Code.ShouldBe(ApiErrorCodes.Conflict);
        }

        (await world.Connection.QuerySingleAsync<string>("SELECT status FROM retro_theme_merge_proposal WHERE id = @merged", new { merged })).ShouldBe("merged");
        (await world.Connection.QuerySingleAsync<string>("SELECT status FROM retro_theme_merge_proposal WHERE id = @kept", new { kept })).ShouldBe("keptApart");
    }

    [Test]
    public async Task KeepApart_ShouldHideItAndBlockAReproposal()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var (alpha, beta) = (await world.Seed.ThemeAsync("Alpha"), await world.Seed.ThemeAsync("Beta"));
        var retro = await world.AgentRetroAsync();
        var point = await world.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "One.", ThemeId: alpha));
        await ProposeAsync(world, "Alpha", "Beta");
        var id = (await PendingAsync(world.Project.Id)).Single().Id;

        using var response = await Api.PostJsonAsync($"/api/retros/theme-merge-proposals/{id}/keep-apart");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await PendingAsync(world.Project.Id)).ShouldBeEmpty();
        var stored = await world.Connection.QuerySingleAsync<(string Status, Guid? By)>("SELECT status, decided_by_user_id AS By FROM retro_theme_merge_proposal WHERE id = @id", new { id });
        stored.ShouldBe(("keptApart", SeedIds.Users.Joel));
        (await world.Connection.QuerySingleAsync<Guid?>("SELECT theme_id FROM retro_point WHERE id = @point", new { point })).ShouldBe(alpha);
        (await world.Connection.QuerySingleAsync<Guid?>("SELECT merged_into_id FROM retro_theme WHERE id = @alpha", new { alpha })).ShouldBeNull();
        (await ProposeAsync(world, "Alpha", "Beta")).ShouldBeFalse();
        (await ProposeAsync(world, "Beta", "Alpha")).ShouldBeFalse();
        (await PendingAsync(world.Project.Id)).ShouldBeEmpty();
        (alpha, beta).ShouldNotBe((Guid.Empty, Guid.Empty));
    }

    [Test]
    public async Task Pending_WhenThemeMergedElsewhere_ShouldLeaveItOut()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var alpha = await world.Seed.ThemeAsync("Alpha");
        await world.Seed.ThemeAsync("Beta");
        await world.Seed.ThemeAsync("Gamma");
        await world.Seed.ThemeAsync("Delta");
        var elsewhere = await world.Seed.ThemeAsync("Elsewhere");
        await ProposeAsync(world, "Alpha", "Beta");
        await ProposeAsync(world, "Gamma", "Delta");
        (await PendingAsync(world.Project.Id)).Count.ShouldBe(2);

        using var merge = await Api.PostJsonAsync($"/api/projects/{world.Project.Id}/retros/themes/merge", new { sourceIds = new[] { alpha }, targetId = elsewhere });

        merge.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var left = await PendingAsync(world.Project.Id);
        left.Select(proposal => (proposal.From.Name, proposal.Into.Name)).ShouldBe([("Gamma", "Delta")]);
    }

    [Test]
    public async Task Pending_WhenOtherProjectProposed_ShouldListOnlyThisProjects()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        await using var other = await World.CreateAsync(await OpenDatabaseAsync());
        await world.Seed.ThemeAsync("Alpha");
        await world.Seed.ThemeAsync("Beta");
        await other.Seed.ThemeAsync("Alpha");
        await other.Seed.ThemeAsync("Beta");
        await ProposeAsync(world, "Alpha", "Beta");
        await ProposeAsync(other, "Alpha", "Beta");

        (await PendingAsync(world.Project.Id)).Count.ShouldBe(1);
    }

    [Test]
    public async Task Merge_WhenOtherProject_ShouldBeNotFound()
    {
        await using var connection = await OpenDatabaseAsync();
        var project = await ArrangedProject.CreateAsync(connection);
        var seed = new RetroSeed(connection, project.Id);
        await seed.ThemeAsync("Alpha");
        await seed.ThemeAsync("Beta");
        using (var scope = Host.Services.CreateScope())
        {
            (await scope.ServiceProvider.GetRequiredService<IRetroDecisionWrites>().ProposeMergeAsync(project.Id, "Alpha", "Beta", "Same.", Agent)).ShouldBeTrue();
        }

        var id = await connection.QuerySingleAsync<Guid>("SELECT id FROM retro_theme_merge_proposal WHERE project_id = @project", new { project = project.Id });
        foreach (var route in new[] { "merge", "keep-apart" })
        {
            using var response = await Api.PostJsonAsync($"/api/retros/theme-merge-proposals/{id}/{route}");
            (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
        }

        (await connection.QuerySingleAsync<string>("SELECT status FROM retro_theme_merge_proposal WHERE id = @id", new { id })).ShouldBe("pending");
    }

    [Test]
    public async Task Insights_WhenProposalPending_ShouldListItInBothModes()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        await world.Seed.ThemeAsync("Alpha");
        await world.Seed.ThemeAsync("Beta");
        await ProposeAsync(world, "Alpha", "Beta");
        var pending = (await PendingAsync(world.Project.Id)).Single();
        using var scope = Host.Services.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IRetroInsightsReader>();

        var sprints = await reader.GetAsync(world.Project.Id, RetroRange.Last6, CancellationToken.None);
        var weeks = await reader.GetAsync(world.Project.Id, RetroRange.Weeks6, CancellationToken.None);

        sprints.MergeProposals.ShouldBe([pending]);
        weeks.MergeProposals.ShouldBe([pending]);
        pending.From.Name.ShouldBe("Alpha");
        pending.Reason.ShouldBe("Same.");
        pending.ProposedBy.AgentName.ShouldBe("claude-x");
    }

    private async Task<bool> ProposeAsync(World world, string from, string into)
    {
        using var scope = Host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IRetroDecisionWrites>().ProposeMergeAsync(world.Project.Id, from, into, "Same.", Agent);
    }

    private async Task<IReadOnlyList<RetroMergeProposalDto>> PendingAsync(Guid projectId)
    {
        using var scope = Host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IRetroMergeProposals>().PendingAsync(projectId);
    }

    private static Task<Guid> ProposalIdAsync(World world, string from)
        => world.Connection.QuerySingleAsync<Guid>("SELECT p.id FROM retro_theme_merge_proposal p JOIN retro_theme t ON t.id = p.from_theme_id WHERE p.project_id = @project AND t.name = @from", new { project = world.Project.Id, from });
}
