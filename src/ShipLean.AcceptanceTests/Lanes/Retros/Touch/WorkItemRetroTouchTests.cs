using System.Text.RegularExpressions;
using Dapper;
using Npgsql;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Retros;
using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Retros;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Retros.Touch;

// The Retrospectives rows on a work item, on a phone (SHIP-729 R8): one two-line row link per retro, no sideways scroll.
[TestFixture]
public class WorkItemRetroTouchTests : SeededAcceptanceTest
{
    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    [Test]
    public async Task RetroRows_WhenTheItemIsOpenedOnAPhone_ShouldShowTwoLineRowsThatOpenTheRetro()
    {
        var (key, agentRetro) = await ArrangeAsync();
        await Page.GotoAsync(Links.WorkItem("SHIP", key));

        var section = new WorkItemRetroSection(Page);
        await Expect(section.Rows).ToHaveCountAsync(2);
        (await section.Section.EvaluateAsync<bool>("e => e.scrollWidth <= e.clientWidth")).ShouldBeTrue();
        (await section.Section.EvaluateAsync<bool>("e => e.getBoundingClientRect().right <= document.documentElement.clientWidth")).ShouldBeTrue();
        for (var i = 0; i < 2; i++)
        {
            var row = section.Rows.Nth(i);
            (await row.GetAttributeAsync("href"))!.ShouldContain($"/retros/items/{key}?retro=");
            (await row.BoundingBoxAsync())!.Height.ShouldBeGreaterThanOrEqualTo(32);
            await Expect(row.GetByTestId(RetroSharedTestIds.RetroAuthorChip)).ToHaveCountAsync(1);
            await Expect(row.GetByTestId(RetrosTestIds.RetroCategoryCount)).ToHaveCountAsync(4);
            (await TouchRules.ViolationsAsync(Page, row)).ShouldBeEmpty();
        }

        var agentRow = section.Rows.Filter(new LocatorFilterOptions { HasText = "agent-row" });
        await Expect(agentRow).ToHaveCountAsync(1);
        await agentRow.GetByTestId(RetrosTestIds.RetroCategoryCount).Last.TapAsync();
        await Expect(Page).ToHaveURLAsync(new Regex($"/retros/items/{key}\\?retro={agentRetro}$"));
    }

    [TearDown]
    public async Task SettleSeededPointsAsync()
    {
        await using var connection = await OpenConnectionAsync();
        await connection.ExecuteAsync("UPDATE retro_point SET archived_at = now() WHERE archived_at IS NULL AND text LIKE 'Phone row %'");
    }

    // A story with two retros, one by an agent and one by a person.
    private async Task<(string Key, Guid AgentRetro)> ArrangeAsync()
    {
        await using var connection = await OpenConnectionAsync();
        var id = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithTitle($"Phone retro rows {Guid.NewGuid():N}").InsertAsync(connection);
        var seed = new RetroSeed(connection, SeedIds.Projects.Ship);
        var agent = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, RetroRole.SubAgent, WorkItemId: id, AgentName: "agent-row", CreatedAt: new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero)));
        await seed.PointAsync(agent, new RetroSeedPoint(RetroCategory.Improve, "Phone row improve"));
        var person = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, RetroRole.Person, WorkItemId: id, CreatedAt: new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero)));
        await seed.PointAsync(person, new RetroSeedPoint(RetroCategory.WentWell, "Phone row well"));
        var key = (await connection.ExecuteScalarAsync<string>("SELECT key FROM work_item WHERE id = @id", new { id }))!;
        return (key, agent);
    }

    private async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        return connection;
    }
}
