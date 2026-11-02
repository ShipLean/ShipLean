using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Retros.Reads;
using ShipLean.Contracts.Retros;
using ShipLean.Core;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Retros;

namespace ShipLean.WebApi.Tests.Retros.Reads;

// Contract K2: a point's decision, read through IRetroCards (the one reader of the rule) and through the RetroDecisionSql fragment.
public sealed class RetroCardsDecisionTests : ApiFixture
{
    private static readonly Guid Project = SeedIds.Projects.Ship;

    private IRetroCards Cards => Host.Services.GetRequiredService<IRetroCards>();

    [Test]
    public async Task Load_WhenLegacyActionHasPoint_ShouldShowActionAndActioned()
    {
        await using var connection = await OpenDatabaseAsync();
        var seed = new RetroSeed(connection, Project);
        var retro = await AgentRetroAsync(seed);
        var point = await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.Improve, "Do the thing."));
        var item = await ActionItemAsync(connection);
        await connection.ExecuteAsync("INSERT INTO retro_action (work_item_id, project_id, point_id) VALUES (@item, @Project, @point)", new { item, Project, point });
        await connection.ExecuteAsync("INSERT INTO retro_action_point (work_item_id, point_id) SELECT work_item_id, point_id FROM retro_action WHERE work_item_id = @item", new { item });

        var loaded = (await Cards.LoadAsync([retro], CancellationToken.None)).Single().Points.Single();

        loaded.Decision.ShouldBe(RetroDecision.Actioned);
        loaded.NotNeeded.ShouldBeNull();
        var action = loaded.Action.ShouldNotBeNull();
        action.WorkItemId.ShouldBe(item);
        action.FixType.ShouldBeNull();
        action.PointCount.ShouldBe(1);
    }

    [Test]
    public async Task Load_WhenOneActionCoversTwoPoints_ShouldGiveBothTheActionWithItsFixTypeAndCount()
    {
        await using var connection = await OpenDatabaseAsync();
        var seed = new RetroSeed(connection, Project);
        var retro = await AgentRetroAsync(seed);
        var first = await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.Improve, "State it."));
        var second = await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "Not stated."));
        var gone = await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.NotInPlan, "Archived later."));
        var item = await ActionItemAsync(connection);
        await seed.ActionAsync(new RetroSeedAction(item, FixType: RetroFixType.Guide, PointIds: [first, second, gone]));
        await connection.ExecuteAsync("UPDATE retro_point SET archived_at = now() WHERE id = @gone", new { gone });

        var points = (await Cards.LoadAsync([retro], CancellationToken.None)).Single().Points;

        points.Select(point => point.Decision).ShouldBe([RetroDecision.Actioned, RetroDecision.Actioned]);
        points.ShouldAllBe(point => point.Action!.WorkItemId == item && point.Action.FixType == RetroFixType.Guide && point.Action.PointCount == 2);
    }

    [Test]
    public async Task Load_WhenActionItemRemoved_ShouldBeUndecidedWithNoAction()
    {
        await using var connection = await OpenDatabaseAsync();
        var seed = new RetroSeed(connection, Project);
        var retro = await AgentRetroAsync(seed);
        var point = await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.Improve, "Do the thing."));
        var item = await ActionItemAsync(connection);
        await seed.ActionAsync(new RetroSeedAction(item, PointId: point));
        (await Cards.LoadAsync([retro], CancellationToken.None)).Single().Points.Single().Decision.ShouldBe(RetroDecision.Actioned);
        await connection.ExecuteAsync(
            "UPDATE work_item SET state_id = (SELECT id FROM state WHERE project_id = @Project AND type = 'action' AND category = 'removed' LIMIT 1) WHERE id = @item",
            new { Project, item });

        var loaded = (await Cards.LoadAsync([retro], CancellationToken.None)).Single().Points.Single();

        loaded.Decision.ShouldBe(RetroDecision.Undecided);
        loaded.Action.ShouldBeNull();
        (await DecisionSqlAsync(connection, point)).ShouldBe("undecided");
    }

    [Test]
    public async Task Load_WhenNotNeeded_ShouldCarryReasonAndWho()
    {
        await using var connection = await OpenDatabaseAsync();
        var seed = new RetroSeed(connection, Project);
        var retro = await AgentRetroAsync(seed);
        var decidedAt = Host.Services.GetRequiredService<TimeProvider>().GetUtcNow().AddDays(-2);
        var point = await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.NotInPlan, "Surprise.", NotNeededReason: "One-off.", DecidedById: SeedIds.Users.Priya, DecidedAt: decidedAt));
        var byAgent = await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "Again.", NotNeededReason: "Covered.", DecidedById: SeedIds.Users.Joel, DecidedAgentName: "decide-agent"));

        var points = (await Cards.LoadAsync([retro], CancellationToken.None)).Single().Points;

        var person = points.Single(p => p.Id == point);
        person.Decision.ShouldBe(RetroDecision.NotNeeded);
        person.Action.ShouldBeNull();
        var notNeeded = person.NotNeeded.ShouldNotBeNull();
        notNeeded.Reason.ShouldBe("One-off.");
        notNeeded.By.User.Id.ShouldBe(SeedIds.Users.Priya);
        notNeeded.By.AgentName.ShouldBeNull();
        notNeeded.By.Role.ShouldBe(RetroRole.Person);
        notNeeded.DecidedAtLabel.ShouldBe(DisplayDate.Relative(decidedAt, Host.Services.GetRequiredService<TimeProvider>()));
        points.Single(p => p.Id == byAgent).NotNeeded.ShouldNotBeNull().By.AgentName.ShouldBe("decide-agent");
        (await DecisionSqlAsync(connection, point)).ShouldBe("notNeeded");
    }

    [Test]
    public async Task Load_WhenWentWell_ShouldHaveNullDecision()
    {
        await using var connection = await OpenDatabaseAsync();
        var seed = new RetroSeed(connection, Project);
        var retro = await AgentRetroAsync(seed);
        var point = await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentWell, "Smooth."));

        var loaded = (await Cards.LoadAsync([retro], CancellationToken.None)).Single().Points.Single();

        loaded.Decision.ShouldBeNull();
        loaded.NotNeeded.ShouldBeNull();
        (await DecisionSqlAsync(connection, point)).ShouldBeNull();
    }

    [Test]
    public async Task Load_WhenNothingDecided_ShouldBeUndecided()
    {
        await using var connection = await OpenDatabaseAsync();
        var seed = new RetroSeed(connection, Project);
        var retro = await AgentRetroAsync(seed);
        var point = await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.Improve, "Open."));

        (await Cards.LoadAsync([retro], CancellationToken.None)).Single().Points.Single().Decision.ShouldBe(RetroDecision.Undecided);
        (await DecisionSqlAsync(connection, point)).ShouldBe("undecided");
    }

    private static async Task<string?> DecisionSqlAsync(Npgsql.NpgsqlConnection connection, Guid point)
        => await connection.QuerySingleAsync<string?>($"SELECT {RetroDecisionSql.Decision("p")} FROM retro_point p WHERE p.id = @point", new { point });

    private static Task<Guid> ActionItemAsync(Npgsql.NpgsqlConnection connection)
        => new WorkItemBuilder(Project).WithType("action").WithTitle($"Fix {Guid.NewGuid():N}").InsertAsync(connection);

    private static Task<Guid> AgentRetroAsync(RetroSeed seed)
        => seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, WorkItemId: SeedIds.WorkItems.Ship92, AgentName: $"agent-{Guid.NewGuid():N}"));
}
