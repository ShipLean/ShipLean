using System.Net;
using Dapper;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views;
using ShipLean.Core;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.Backlog.Stories;

namespace ShipLean.WebApi.Tests.Backlog.Levels;

// The Features and Epics backlogs over the seeded SHIP project: roll-ups, nested Features, the sum warning.
[TestFixture]
public class LevelBacklogRollupApiTests : ApiFixture
{
    private static readonly string Features = $"api/projects/{SeedIds.Projects.Ship}/backlogs/features?includeCompleted=true";
    private static readonly string Epics = $"api/projects/{SeedIds.Projects.Ship}/backlogs/epics?includeCompleted=true";

    private static RollupCountDto Count(BacklogRowDto row, WorkItemTypeId type) => row.Rollups.Single(r => r.Type == type);

    [Test]
    public async Task Features_WhenNestedFeature_ShouldRollUpProgress()
    {
        var view = await Api.GetBacklogAsync(Features);

        view.Level.ShouldBe(ShipLean.Contracts.Boards.BoardLevel.Features);
        view.Rows.Select(row => row.Key).ShouldBe(["SHIP-101", "SHIP-102", "SHIP-103", "SHIP-108", "SHIP-104"]);
        view.Rows.Select(row => row.Rank).ShouldBe([1, 2, 3, 4, 5]);
        var nested = view.Rows.Single(row => row.Key == "SHIP-108");
        nested.Parent.ShouldNotBeNull().Key.ShouldBe("SHIP-103");
        Count(nested, WorkItemTypeId.Story).ShouldBe(new RollupCountDto(WorkItemTypeId.Story, 2, 0, 0, 0));
        var parent = view.Rows.Single(row => row.Key == "SHIP-103");
        Count(parent, WorkItemTypeId.Story).ShouldBe(new RollupCountDto(WorkItemTypeId.Story, 4, 0, 0, 1));
        Count(parent, WorkItemTypeId.Bug).ShouldBe(new RollupCountDto(WorkItemTypeId.Bug, 0, 1, 0, 0));
        Count(view.Rows.Single(row => row.Key == "SHIP-101"), WorkItemTypeId.Story).ShouldBe(new RollupCountDto(WorkItemTypeId.Story, 3, 3, 2, 0));
        view.Rows.ShouldAllBe(row => row.Rollups.All(r => r.Type != WorkItemTypeId.Task));
    }

    [Test]
    public async Task Epics_WhenListed_ShouldCountNestedFeatures()
    {
        var view = await Api.GetBacklogAsync(Epics);

        view.Level.ShouldBe(ShipLean.Contracts.Boards.BoardLevel.Epics);
        view.Rows.Select(row => row.Key).ShouldBe(["SHIP-90", "SHIP-91", "SHIP-92", "SHIP-93"]);
        var withNested = view.Rows.Single(row => row.Key == "SHIP-92");
        Count(withNested, WorkItemTypeId.Feature).ShouldBe(new RollupCountDto(WorkItemTypeId.Feature, 2, 0, 0, 0));
        Count(withNested, WorkItemTypeId.Story).ShouldBe(new RollupCountDto(WorkItemTypeId.Story, 4, 0, 0, 1));
        Count(view.Rows.Single(row => row.Key == "SHIP-90"), WorkItemTypeId.Feature).Open.ShouldBe(1);
    }

    // Built in the empty seeded project, so the SHIP rows the other tests read are untouched.
    [Test]
    public async Task Features_WhenStoriesInSprints_ShouldCarrySprintCounts()
    {
        var project = SeedIds.Projects.Empt;
        var today = SprintLabels.Today(TimeProvider.System);
        await using var connection = await OpenDatabaseAsync();
        var current = Guid.NewGuid();
        var next = Guid.NewGuid();
        await connection.ExecuteAsync(
            "INSERT INTO sprint (id, project_id, name, start_date, end_date) VALUES (@current, @project, 'Rollup current', @start, @end), (@next, @project, 'Rollup next', @nextStart, @nextEnd)",
            new { current, next, project, start = today.AddDays(-3), end = today.AddDays(3), nextStart = today.AddDays(4), nextEnd = today.AddDays(17) });
        var outer = await new WorkItemBuilder(project).WithType("feature").WithTitle("Outer").InsertAsync(connection);
        var inner = await new WorkItemBuilder(project).WithType("feature").WithTitle("Inner").WithParent(outer).InsertAsync(connection);
        await new WorkItemBuilder(project).WithParent(outer).WithSprint(current).InsertAsync(connection);
        await new WorkItemBuilder(project).WithParent(inner).WithSprint(next).InsertAsync(connection);
        await new WorkItemBuilder(project).WithParent(inner).InsertAsync(connection);

        var view = await Api.GetBacklogAsync($"api/projects/{project}/backlogs/features");

        Count(view.Rows.Single(row => row.Id == outer), WorkItemTypeId.Story).ShouldBe(new RollupCountDto(WorkItemTypeId.Story, 3, 0, 1, 1));
        Count(view.Rows.Single(row => row.Id == inner), WorkItemTypeId.Story).ShouldBe(new RollupCountDto(WorkItemTypeId.Story, 2, 0, 0, 1));
    }

    [Test]
    public async Task Features_WhenSumExceedsOwnBy25Percent_ShouldWarn()
    {
        var features = await Api.GetBacklogAsync(Features);
        var epics = await Api.GetBacklogAsync(Epics);

        var warned = features.Rows.Single(row => row.Key == "SHIP-101");
        warned.Points.ShouldBe(20m);
        warned.ChildPoints.ShouldBe(26m);
        warned.SumWarn.ShouldBeTrue();
        // 32 under 30 is over, but not by a quarter.
        var over = epics.Rows.Single(row => row.Key == "SHIP-91");
        over.Points.ShouldBe(30m);
        over.ChildPoints.ShouldBe(32m);
        over.SumWarn.ShouldBeFalse();
        epics.Rows.Single(row => row.Key == "SHIP-90").SumWarn.ShouldBeFalse();
    }

    [TestCase("not json")]
    [TestCase("""{"match":"all","clauses":[null]}""")]
    [TestCase("""{"match":"all","clauses":[{"field":"nope","op":"=","value":"x"}]}""")]
    public async Task LevelBacklog_WhenFilterInvalid_ShouldBeValidation(string filter)
    {
        using var response = await Api.GetAsync($"{Features}&filter={Uri.EscapeDataString(filter)}");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [TestCase("stories")]
    [TestCase("taskboard")]
    public async Task LevelBacklog_WhenLevelIsNotFeaturesOrEpics_ShouldBeValidation(string level)
    {
        using var response = await Api.GetAsync($"api/projects/{SeedIds.Projects.Ship}/backlogs/{level}");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }
}
