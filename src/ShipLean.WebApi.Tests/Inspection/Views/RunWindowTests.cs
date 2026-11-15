using Dapper;
using ShipLean.Application.Features.Inspection.Views;
using ShipLean.Contracts.Inspection;

namespace ShipLean.WebApi.Tests.Inspection.Views;

// K3: every RunQuery and RunRef runs from the item's creation to now, and never further back than SHIPLEAN_INSPECTION_MAX_SEARCH_DAYS.
[TestFixture]
public class RunWindowTests : InspectionViewsFixture
{
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public void For_WhenTheItemIsOlderThanTheCap_ShouldStartAtTheCap()
    {
        var window = RunWindow.For(Now.AddDays(-90), Now, 30);

        window.From.ShouldBe(Now.AddDays(-30));
        window.To.ShouldBe(Now);
    }

    [Test]
    public void For_WhenTheItemIsNewerThanTheCap_ShouldStartAtItsCreation()
    {
        RunWindow.For(Now.AddDays(-3), Now, 30).From.ShouldBe(Now.AddDays(-3));
    }

    [Test]
    public async Task Views_WhenAnItemIsNinetyDaysOld_ShouldSearchOnlyTheLastThirtyDays()
    {
        using var saved = await SetContentAsync(false);
        saved.EnsureSuccessStatusCode();
        await using (var connection = await OpenDatabaseAsync())
        {
            await connection.ExecuteAsync("UPDATE work_item SET created_at = @at WHERE key = @key", new { at = TimeProvider.System.GetUtcNow().AddDays(-90), key = Item });
        }
        Source.Clear();
        var before = TimeProvider.System.GetUtcNow();

        using var section = await Api.GetAsync(RunsUrl);
        using var run = await Api.GetAsync($"{RunsUrl}/{RunId}");

        section.EnsureSuccessStatusCode();
        run.EnsureSuccessStatusCode();
        Source.Queries.ShouldNotBeEmpty();
        Source.Refs.ShouldNotBeEmpty();
        foreach (var (from, to) in Source.Queries.Select(q => (q.From, q.To)).Concat(Source.Refs.Select(r => (r.From, r.To))))
        {
            from.ShouldBeInRange(before.AddDays(-30).AddMinutes(-1), TimeProvider.System.GetUtcNow().AddDays(-30).AddMinutes(1));
            to.ShouldBeInRange(before.AddMinutes(-1), TimeProvider.System.GetUtcNow().AddMinutes(1));
        }
    }
}
