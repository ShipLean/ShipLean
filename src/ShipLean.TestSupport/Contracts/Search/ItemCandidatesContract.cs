using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShipLean.Application.Features.Search.Ranking;
using ShipLean.Contracts.Search;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Database;
using ShipLean.TestSupport.Hosting;
using Shouldly;

namespace ShipLean.TestSupport.Contracts.Search;

// Behaviour every IItemCandidates leg must have, on the seeded database: a user who is not a member of a project sees none of
// its items, removed items (state category removed) are left out, and each list comes best first. The contract creates the work
// items (real rows in PART, where the signed-in user Joel is not a member, and in SHIP, where he is); the subclass says which
// leg, what to ask for and how to write the search_doc rows that make an item match (SearchDocBuilder), so the contract does not
// know the leg's SQL. The subclass derives from this class and so gets one host and one cloned database per class.
[TestFixture]
public abstract class ItemCandidatesContract
{
    private ClonedDatabase _database = null!;
    private ShipLeanHost _host = null!;

    // Doubles go here, as in ApiFixture. Once the timed indexing pass exists (SearchIndexHostedService), a subclass removes it in its
    // override, so a background pass cannot re-sync the arranged rows while a test runs.
    protected virtual Action<IServiceCollection>? ConfigureTestServices => null;

    // The host, for a subclass that needs its services (the embedder, SearchOptions).
    protected ShipLeanHost Host => _host;

    [OneTimeSetUp]
    public async Task StartHost()
    {
        _database = await ShipLeanDatabase.CloneSeedAsync();
        // The name is changed so that only the category identifies the state: a leg that filters on the name "Removed" fails.
        await using (var connection = await _database.OpenAsync())
        {
            await connection.ExecuteAsync("UPDATE state SET name = 'Withdrawn' WHERE id = @id", new { id = SeedIds.States.Ship.StoryRemoved });
        }
        _host = ShipLeanHost.Start(_database.ConnectionString, TestUser.Joel, ConfigureTestServices);
    }

    // Joel acts for every test, as a service called outside a request does.
    [SetUp]
    public void ActAsJoel() => _host.ActAs(TestUser.Joel.Id);

    [OneTimeTearDown]
    public async Task StopHost()
    {
        if (_host != null)
        {
            await _host.DisposeAsync();
        }
        if (_database != null)
        {
            await _database.DisposeAsync();
        }
    }

    protected abstract CandidateLeg Leg { get; }

    // What the leg is asked to find. The arranged rows must match it.
    protected abstract string QueryText { get; }

    // The embedded query for a semantic leg (a semantic leg given null returns Available = false); null for a lexical one.
    protected virtual Task<float[]?> QueryVectorAsync() => Task.FromResult<float[]?>(null);

    // Writes the search_doc rows of the work item (the item exists, titled with the QueryText) so that it matches QueryText.
    // A Strong item must score above a Weak one, so both strengths must land in at least one common list (the same field), above
    // the leg's cut-off; the contract fails when no list holds both. Rows go in the item's own project.
    protected abstract Task ArrangeMatchAsync(NpgsqlConnection connection, Guid projectId, Guid workItemId, string itemTitle, MatchStrength strength);

    private static readonly Guid Member = SeedIds.Projects.Ship;
    private static readonly Guid NotMember = SeedIds.Projects.Part;

    [Test]
    public async Task Lists_WhenNotMember_ShouldSeeNothing()
    {
        var hidden = await ArrangeItemAsync(NotMember, MatchStrength.Strong);
        var shown = await ArrangeItemAsync(Member, MatchStrength.Strong);

        var found = await HitIdsAsync(Member, NotMember);

        found.ShouldContain(shown);
        found.ShouldNotContain(hidden);
    }

    [Test]
    public async Task Lists_WhenItemRemoved_ShouldLeaveItOut()
    {
        var removed = await ArrangeItemAsync(Member, MatchStrength.Strong, stateId: SeedIds.States.Ship.StoryRemoved);
        // The writer keeps the comment rows of a removed item (docs/search.md); the leg must hide them through the join.
        await using (var connection = await _database.OpenAsync())
        {
            await new SearchDocBuilder(Member, SearchSourceKind.Comment, Guid.NewGuid(), SearchField.Comment, $"{QueryText} comment").WithWorkItem(removed).InsertAsync(connection);
        }
        var kept = await ArrangeItemAsync(Member, MatchStrength.Strong);

        var found = await HitIdsAsync(Member);

        found.ShouldContain(kept);
        found.ShouldNotContain(removed);
    }

    [Test]
    public async Task Lists_ShouldComeBestFirst()
    {
        var weak = await ArrangeItemAsync(Member, MatchStrength.Weak);
        var strong = await ArrangeItemAsync(Member, MatchStrength.Strong);

        var result = await ListsAsync(Member);

        result.Available.ShouldBeTrue();
        var withBoth = result.Lists.Where(l => l.Hits.Any(h => h.WorkItemId == strong) && l.Hits.Any(h => h.WorkItemId == weak)).ToList();
        withBoth.ShouldNotBeEmpty("no list holds both the strong and the weak item; arrange them in the same field");
        foreach (var list in result.Lists)
        {
            list.Hits.Select(h => h.Score).ShouldBe(list.Hits.Select(h => h.Score).OrderDescending(), $"{list.Name} is not best first");
            list.Hits.Select(h => h.WorkItemId).ShouldBeUnique($"{list.Name} has an item twice");
        }
        foreach (var list in withBoth)
        {
            var ids = list.Hits.Select(h => h.WorkItemId).ToList();
            ids.IndexOf(strong).ShouldBeLessThan(ids.IndexOf(weak), $"{list.Name} ranks the weak match first");
        }
    }

    private async Task<CandidateLists> ListsAsync(params Guid[] projectIds)
    {
        using var scope = _host.Services.CreateScope();
        var leg = scope.ServiceProvider.GetServices<IItemCandidates>().Single(c => c.Leg == Leg);
        var query = new CandidateQuery(QueryText, projectIds, Types: null, Tags: null, IncludeClosed: true, Prefix: false, Criteria: null, await QueryVectorAsync());
        return await leg.ListsAsync(query);
    }

    private async Task<HashSet<Guid>> HitIdsAsync(params Guid[] projectIds)
        => (await ListsAsync(projectIds)).Lists.SelectMany(l => l.Hits).Select(h => h.WorkItemId).ToHashSet();

    private async Task<Guid> ArrangeItemAsync(Guid projectId, MatchStrength strength, Guid? stateId = null)
    {
        await using var connection = await _database.OpenAsync();
        var title = $"{QueryText} {Guid.NewGuid():N}";
        var builder = new WorkItemBuilder(projectId).WithTitle(title);
        if (stateId != null)
        {
            builder.WithState(stateId.Value);
        }
        var id = await builder.InsertAsync(connection);
        await ArrangeMatchAsync(connection, projectId, id, title, strength);
        return id;
    }
}
