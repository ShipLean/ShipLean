using ShipLean.Application.Features.Retros.Store;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Retros;
using ShipLean.WebApi.Tests.ProjectSettings.Api;

namespace ShipLean.WebApi.Tests.Retros.Store;

// Finished, rolled over and completed-at, arranged through RetroHistory. The sprint runs Monday 2026-03-02 to Sunday 2026-03-15.
public sealed class RetroSprintFactsTests : ApiFixture
{
    private static readonly DateOnly Start = new(2026, 3, 2);
    private static readonly DateOnly End = new(2026, 3, 15);

    [Test]
    public async Task SprintItems_WhenItemsFinishedAndCarriedAndUntouched_ShouldClassifyEach()
    {
        await using var connection = await OpenDatabaseAsync();
        var project = await ArrangedProject.CreateAsync(connection);
        var sprint = await RetroArrangement.SprintAsync(connection, project.Id, "S1", Start, End);
        var next = await RetroArrangement.SprintAsync(connection, project.Id, "S2", End.AddDays(1), End.AddDays(14));
        var finished = await InSprintAsync(connection, project.Id, "story", sprint);
        await RetroHistory.CompleteAsync(connection, finished, RetroArrangement.Noon(2026, 3, 5));
        var rolledOver = await InSprintAsync(connection, project.Id, "bug", sprint);
        await RetroHistory.MoveToSprintAsync(connection, rolledOver, next, RetroArrangement.Noon(2026, 3, 16));
        var elsewhere = await InSprintAsync(connection, project.Id, "story", next);

        var items = await RetroSprintFacts.SprintItemsAsync(connection, sprint, CancellationToken.None);

        items.ShouldContain(new RetroSprintItem(finished, true, false));
        items.ShouldContain(new RetroSprintItem(rolledOver, false, true));
        items.Select(item => item.ItemId).ShouldNotContain(elsewhere);
        items.Count.ShouldBe(2);
    }

    [Test]
    public async Task Completions_WhenCompletedAfterSprintEnded_ShouldMapToTheSprintItSatIn()
    {
        await using var connection = await OpenDatabaseAsync();
        var project = await ArrangedProject.CreateAsync(connection);
        var sprint = await RetroArrangement.SprintAsync(connection, project.Id, "S1", Start, End);
        var late = await InSprintAsync(connection, project.Id, "story", sprint);
        await RetroHistory.CompleteAsync(connection, late, RetroArrangement.Noon(2026, 3, 20));
        var open = await InSprintAsync(connection, project.Id, "story", sprint);
        var reopened = await InSprintAsync(connection, project.Id, "story", sprint);
        await RetroHistory.CompleteAsync(connection, reopened, RetroArrangement.Noon(2026, 3, 4));
        await RetroHistory.ReopenAsync(connection, reopened, RetroArrangement.Noon(2026, 3, 6));

        var completions = await RetroSprintFacts.CompletionsAsync(connection, [late, open, reopened], CancellationToken.None);

        completions.ShouldBe([new RetroItemCompletion(late, RetroArrangement.Noon(2026, 3, 20), sprint)]);
        (await RetroSprintFacts.SprintItemsAsync(connection, sprint, CancellationToken.None)).Single(item => item.ItemId == late).Finished.ShouldBeFalse();
    }

    private static async Task<Guid> InSprintAsync(Npgsql.NpgsqlConnection connection, Guid projectId, string type, Guid sprintId)
    {
        var id = await RetroHistory.CreateAsync(connection, new WorkItemBuilder(projectId).WithType(type));
        await RetroHistory.MoveToSprintAsync(connection, id, sprintId, RetroArrangement.Noon(2026, 2, 27));
        return id;
    }
}
