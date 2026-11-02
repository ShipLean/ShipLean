using System.Net;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Retros;

namespace ShipLean.WebApi.Tests.Retros.People;

public sealed class RetroDecisionWritesTests : RetroPeopleFixture
{
    private static readonly RetroActor Person = new(SeedIds.Users.Joel, null);

    private static async Task<Guid> PointAsync(World world, RetroCategory category = RetroCategory.Improve, Guid? themeId = null, RetroThemeSetBy? setBy = null)
        => await world.Seed.PointAsync(await world.AgentRetroAsync(), new RetroSeedPoint(category, "A point.", ThemeId: themeId, ThemeSetBy: setBy));

    private async Task<T> InScopeAsync<T>(Func<IRetroDecisionWrites, Task<T>> act)
    {
        using var scope = Host.Services.CreateScope();
        return await act(scope.ServiceProvider.GetRequiredService<IRetroDecisionWrites>());
    }

    private async Task<string> FieldOfValidationAsync(Func<IRetroDecisionWrites, Task> act)
    {
        var error = await Should.ThrowAsync<ApiException>(() => InScopeAsync(async writes =>
        {
            await act(writes);
            return 0;
        }));
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        return error.Field!;
    }

    [Test]
    public async Task MarkNotNeeded_WhenBlankReason_ShouldBeValidation()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var pointId = await PointAsync(world);

        using var response = await Api.PostJsonAsync($"/api/retros/points/{pointId}/not-needed", new { reason = "  " });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Field.ShouldBe("reason");
        (await FieldOfValidationAsync(writes => writes.MarkNotNeededAsync(pointId, new RetroNotNeededPut(" "), Person))).ShouldBe("reason");
    }

    [Test]
    public async Task MarkNotNeeded_WhenWentWell_ShouldBeValidation()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var pointId = await PointAsync(world, RetroCategory.WentWell);

        (await FieldOfValidationAsync(writes => writes.MarkNotNeededAsync(pointId, new RetroNotNeededPut("No."), Person))).ShouldBe("pointId");
    }

    [Test]
    public async Task MarkNotNeeded_WhenAgent_ShouldStoreAgentName()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var pointId = await PointAsync(world);

        var dto = await InScopeAsync(writes => writes.MarkNotNeededAsync(pointId, new RetroNotNeededPut(" One-off. "), new RetroActor(SeedIds.Users.Joel, "claude-x")));

        dto.Decision.ShouldBe(RetroDecision.NotNeeded);
        dto.NotNeeded!.Reason.ShouldBe("One-off.");
        dto.Text.ShouldBe("A point.");
        var stored = await world.Connection.QuerySingleAsync<(Guid By, string Agent)>("SELECT decided_by_user_id AS By, decided_agent_name AS Agent FROM retro_point WHERE id = @pointId", new { pointId });
        stored.ShouldBe((SeedIds.Users.Joel, "claude-x"));
    }

    [Test]
    public async Task MarkNotNeeded_WhenPersonDecidesAgentsPoint_ShouldLeaveTheTextAndStoreNoAgent()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var pointId = await PointAsync(world);

        using var response = await Api.PostJsonAsync($"/api/retros/points/{pointId}/not-needed", new { reason = "Done elsewhere." });

        var body = await ReadAsync(response, HttpStatusCode.OK);
        body.GetProperty("text").GetString().ShouldBe("A point.");
        body.GetProperty("decision").GetString().ShouldBe("notNeeded");
        (await world.Connection.QuerySingleAsync<string?>("SELECT decided_agent_name FROM retro_point WHERE id = @pointId", new { pointId })).ShouldBeNull();
    }

    [Test]
    public async Task MarkNotNeeded_WhenActioned_ShouldConflict()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var pointId = await PointAsync(world);
        await world.Seed.ActionAsync(new RetroSeedAction(await world.ItemAsync("action", inSprint: false), PointId: pointId));

        using var response = await Api.PostJsonAsync($"/api/retros/points/{pointId}/not-needed", new { reason = "No." });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Conflict)).Code.ShouldBe(ApiErrorCodes.Conflict);
        (await world.Connection.QuerySingleAsync<string?>("SELECT not_needed_reason FROM retro_point WHERE id = @pointId", new { pointId })).ShouldBeNull();
    }

    [Test]
    public async Task MarkNotNeeded_WhenAlreadyNotNeeded_ShouldConflict()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var pointId = await world.Seed.PointAsync(await world.AgentRetroAsync(), new RetroSeedPoint(RetroCategory.Improve, "Twice.", NotNeededReason: "First."));

        using var response = await Api.PostJsonAsync($"/api/retros/points/{pointId}/not-needed", new { reason = "Second." });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Conflict)).Code.ShouldBe(ApiErrorCodes.Conflict);
        (await world.Connection.QuerySingleAsync<string>("SELECT not_needed_reason FROM retro_point WHERE id = @pointId", new { pointId })).ShouldBe("First.");
    }

    [Test]
    public async Task UndoNotNeeded_ShouldReturnPointToUndecided()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var pointId = await world.Seed.PointAsync(await world.AgentRetroAsync(), new RetroSeedPoint(RetroCategory.Improve, "Back.", NotNeededReason: "Meh.", DecidedAgentName: "claude-x"));

        using var response = await Api.PostJsonAsync($"/api/retros/points/{pointId}/undo-not-needed");

        (await ReadAsync(response, HttpStatusCode.OK)).GetProperty("decision").GetString().ShouldBe("undecided");
        var row = await world.Connection.QuerySingleAsync<(string? Reason, Guid? By, string? Agent, DateTimeOffset? At)>(
            "SELECT not_needed_reason AS Reason, decided_by_user_id AS By, decided_agent_name AS Agent, decided_at AS At FROM retro_point WHERE id = @pointId", new { pointId });
        row.ShouldBe((null, null, null, null));
    }

    [Test]
    public async Task UndoNotNeeded_WhenUndecided_ShouldConflict()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var pointId = await PointAsync(world);

        using var response = await Api.PostJsonAsync($"/api/retros/points/{pointId}/undo-not-needed");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Conflict)).Code.ShouldBe(ApiErrorCodes.Conflict);
    }

    [Test]
    public async Task AssignTheme_WhenPersonSetTheme_ShouldChangeNothing()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var mine = await world.Seed.ThemeAsync("Mine");
        var pointId = await PointAsync(world, themeId: mine, setBy: RetroThemeSetBy.Person);

        var assigned = await InScopeAsync(writes => writes.AssignThemeAsync(pointId, "Other", new RetroActor(SeedIds.Users.Joel, "claude-x")));

        assigned.ShouldBeFalse();
        (await world.Connection.QuerySingleAsync<Guid>("SELECT theme_id FROM retro_point WHERE id = @pointId", new { pointId })).ShouldBe(mine);
        (await world.Connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM retro_theme WHERE project_id = @project", new { project = world.Project.Id })).ShouldBe(1);
    }

    [Test]
    public async Task AssignTheme_ShouldKeepPointCountsRight()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var old = await world.Seed.ThemeAsync("Old");
        var target = await world.Seed.ThemeAsync("Target");
        var pointId = await PointAsync(world, themeId: old, setBy: RetroThemeSetBy.Agent);

        var assigned = await InScopeAsync(writes => writes.AssignThemeAsync(pointId, "target", new RetroActor(SeedIds.Users.Joel, "claude-x")));
        var created = await InScopeAsync(writes => writes.AssignThemeAsync(pointId, "Brand new", new RetroActor(SeedIds.Users.Joel, "claude-x")));

        assigned.ShouldBeTrue();
        created.ShouldBeTrue();
        var counts = (await world.Connection.QueryAsync<(string Name, int Count)>("SELECT name, point_count AS Count FROM retro_theme WHERE project_id = @project ORDER BY name", new { project = world.Project.Id })).ToList();
        counts.ShouldBe([("Brand new", 1), ("Old", 0), ("Target", 0)]);
        (await world.Connection.QuerySingleAsync<string>("SELECT theme_set_by FROM retro_point WHERE id = @pointId", new { pointId })).ShouldBe("agent");
        target.ShouldNotBe(Guid.Empty);
    }

    [Test]
    public async Task ProposeMerge_ShouldStorePendingProposalWithActor()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        await world.Seed.ThemeAsync("Alpha");
        await world.Seed.ThemeAsync("Beta");

        var proposed = await InScopeAsync(writes => writes.ProposeMergeAsync(world.Project.Id, "alpha", "Beta", " Same thing. ", new RetroActor(SeedIds.Users.Joel, "claude-x")));

        proposed.ShouldBeTrue();
        var row = await world.Connection.QuerySingleAsync<(string Status, string Reason, string Agent)>(
            "SELECT status, reason, proposed_agent_name AS Agent FROM retro_theme_merge_proposal WHERE project_id = @project", new { project = world.Project.Id });
        row.ShouldBe(("pending", "Same thing.", "claude-x"));
    }

    [Test]
    public async Task ProposeMerge_WhenPairReversed_ShouldReturnFalse()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var (alpha, beta) = (await world.Seed.ThemeAsync("Alpha"), await world.Seed.ThemeAsync("Beta"));
        (await InScopeAsync(writes => writes.ProposeMergeAsync(world.Project.Id, "Alpha", "Beta", "Same.", Person))).ShouldBeTrue();
        await world.Connection.ExecuteAsync("UPDATE retro_theme_merge_proposal SET status = 'keptApart'");

        (await InScopeAsync(writes => writes.ProposeMergeAsync(world.Project.Id, "Beta", "Alpha", "Again.", Person))).ShouldBeFalse();
        (await InScopeAsync(writes => writes.ProposeMergeAsync(world.Project.Id, "Alpha", "Beta", "Again.", Person))).ShouldBeFalse();

        (await world.Connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM retro_theme_merge_proposal WHERE project_id = @project", new { project = world.Project.Id })).ShouldBe(1);
        (alpha, beta).ShouldNotBe((Guid.Empty, Guid.Empty));
    }

    [Test]
    public async Task ProposeMerge_WhenThemeUnknown_ShouldBeValidation()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        await world.Seed.ThemeAsync("Alpha");

        (await FieldOfValidationAsync(writes => writes.ProposeMergeAsync(world.Project.Id, "Nope", "Alpha", "Why.", Person))).ShouldBe("fromTheme");
        (await FieldOfValidationAsync(writes => writes.ProposeMergeAsync(world.Project.Id, "Alpha", "Nope", "Why.", Person))).ShouldBe("intoTheme");
    }

    [Test]
    public async Task ProposeMerge_WhenSameTheme_ShouldBeValidation()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        await world.Seed.ThemeAsync("Alpha");

        (await FieldOfValidationAsync(writes => writes.ProposeMergeAsync(world.Project.Id, "Alpha", "alpha", "Why.", Person))).ShouldBe("intoTheme");
    }

    [Test]
    public async Task ProposeMerge_WhenThemeInOtherProject_ShouldBeValidation()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        await using var other = await World.CreateAsync(await OpenDatabaseAsync());
        await world.Seed.ThemeAsync("Alpha");
        await other.Seed.ThemeAsync("Beta");

        (await FieldOfValidationAsync(writes => writes.ProposeMergeAsync(world.Project.Id, "Alpha", "Beta", "Why.", Person))).ShouldBe("intoTheme");
    }

    [Test]
    public async Task Writes_WhenPointInOtherProject_ShouldBeNotFound()
    {
        await using var world = await World.CreateAsync(await OpenDatabaseAsync());
        var pointId = await PointAsync(world);
        Host.ActAs(SeedIds.Users.Kai);
        var access = Host.Services.GetRequiredService<IProjectAccess>();

        var refused = await Should.ThrowAsync<ApiException>(() => access.RequireAsync(ProjectItem.RetroPoint, pointId, ProjectRole.Member));
        refused.Code.ShouldBe(ApiErrorCodes.NotFound);
        var missing = await Should.ThrowAsync<ApiException>(() => InScopeAsync(writes => writes.AssignThemeAsync(Guid.NewGuid(), "Any", Person)));
        missing.Code.ShouldBe(ApiErrorCodes.NotFound);
        (await world.Connection.QuerySingleAsync<string?>("SELECT not_needed_reason FROM retro_point WHERE id = @pointId", new { pointId })).ShouldBeNull();
    }
}
