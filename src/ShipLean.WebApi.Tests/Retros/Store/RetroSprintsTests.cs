using Npgsql;
using ShipLean.Application.Features.Retros.Store;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Retros;
using ShipLean.WebApi.Tests.ProjectSettings.Api;

namespace ShipLean.WebApi.Tests.Retros.Store;

// The sprint attribution rule (C2) against an arranged project of its own.
public sealed class RetroSprintsTests : ApiFixture
{
    private static readonly DateOnly Start = new(2026, 3, 2);
    private static readonly DateOnly End = new(2026, 3, 15);

    [Test]
    public async Task Resolve_WhenStoryInSprint_ShouldGiveThatSprint()
    {
        await using var connection = await OpenDatabaseAsync();
        var project = await ArrangedProject.CreateAsync(connection);
        var sprint = await RetroArrangement.SprintAsync(connection, project.Id, "S1", Start, End);
        var story = await RetroHistory.CreateAsync(connection, new WorkItemBuilder(project.Id).WithType("story").WithSprint(sprint));

        (await ResolveAsync(connection, story, null)).ShouldBe(sprint);
    }

    [Test]
    public async Task Resolve_WhenStoryInNoSprint_ShouldBeNull()
    {
        await using var connection = await OpenDatabaseAsync();
        var project = await ArrangedProject.CreateAsync(connection);
        var story = await RetroHistory.CreateAsync(connection, new WorkItemBuilder(project.Id).WithType("story"));

        (await ResolveAsync(connection, story, null)).ShouldBeNull();
    }

    [Test]
    public async Task Resolve_WhenSprintId_ShouldGiveItself()
    {
        await using var connection = await OpenDatabaseAsync();
        var project = await ArrangedProject.CreateAsync(connection);
        var sprint = await RetroArrangement.SprintAsync(connection, project.Id, "S1", Start, End);

        (await ResolveAsync(connection, null, sprint)).ShouldBe(sprint);
    }

    [Test]
    public async Task Resolve_WhenFeature_ShouldGiveSprintOfLastCompletedChild()
    {
        await using var connection = await OpenDatabaseAsync();
        var project = await ArrangedProject.CreateAsync(connection);
        var first = await RetroArrangement.SprintAsync(connection, project.Id, "S1", Start, End);
        var second = await RetroArrangement.SprintAsync(connection, project.Id, "S2", End.AddDays(1), End.AddDays(14));
        var feature = await RetroHistory.CreateAsync(connection, new WorkItemBuilder(project.Id).WithType("feature"));
        var early = await RetroHistory.CreateAsync(connection, new WorkItemBuilder(project.Id).WithType("story").WithParent(feature));
        var late = await RetroHistory.CreateAsync(connection, new WorkItemBuilder(project.Id).WithType("story").WithParent(feature));
        var open = await RetroHistory.CreateAsync(connection, new WorkItemBuilder(project.Id).WithType("story").WithParent(feature));
        await RetroHistory.MoveToSprintAsync(connection, early, first, RetroArrangement.Noon(2026, 3, 2));
        await RetroHistory.CompleteAsync(connection, early, RetroArrangement.Noon(2026, 3, 5));
        await RetroHistory.MoveToSprintAsync(connection, late, second, RetroArrangement.Noon(2026, 3, 16));
        await RetroHistory.CompleteAsync(connection, late, RetroArrangement.Noon(2026, 3, 18));
        await RetroHistory.MoveToSprintAsync(connection, open, first, RetroArrangement.Noon(2026, 3, 3));

        (await ResolveAsync(connection, feature, null)).ShouldBe(second);
    }

    [Test]
    public async Task Resolve_WhenFeatureWithNoCompletedChild_ShouldBeNull()
    {
        await using var connection = await OpenDatabaseAsync();
        var project = await ArrangedProject.CreateAsync(connection);
        var feature = await RetroHistory.CreateAsync(connection, new WorkItemBuilder(project.Id).WithType("feature"));
        await RetroHistory.CreateAsync(connection, new WorkItemBuilder(project.Id).WithType("story").WithParent(feature));

        (await ResolveAsync(connection, feature, null)).ShouldBeNull();
    }

    private static Task<Guid?> ResolveAsync(NpgsqlConnection connection, Guid? workItemId, Guid? sprintId)
        => RetroSprints.ResolveAsync(connection, workItemId, sprintId, CancellationToken.None);
}
