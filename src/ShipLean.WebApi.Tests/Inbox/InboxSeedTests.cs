using Dapper;
using ShipLean.LocalEnvironment.Database;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Inbox;

namespace ShipLean.WebApi.Tests.Inbox;

// Tom's Inbox seed (db/seed/0013_inbox.sql, SeedIds.Inbox): read only.
[TestFixture]
public class InboxSeedTests
{
    private FreshDatabase _database = null!;

    [OneTimeSetUp]
    public async Task SetUp() => _database = await FreshDatabase.CreateAsync();

    [OneTimeTearDown]
    public async Task TearDown() => await _database.DisposeAsync();

    // SHIP-1093: red until the seed makes SeedIds.Inbox.StaleFeature Tom's and stale (see the TODO on SeedIds.Inbox).
    [Test]
    public async Task Seed_WhenLoaded_ShouldGiveTomTheInboxSeed()
    {
        await using var connection = await _database.OpenAsync();

        var fresh = await FeatureAsync(SeedIds.Inbox.FreshFeature);
        fresh.ShouldBe(("feature", SeedIds.Users.Tom, (DateTimeOffset?)TestDates.Today));
        var mentions = (await InboxRows.MentionsAsync(connection, SeedIds.Inbox.OpenMentionComment))
            .Concat(await InboxRows.MentionsAsync(connection, SeedIds.Inbox.SeenMentionComment)).ToList();
        mentions.Select(m => (m.Id, m.UserId, m.WorkItemId, m.State, m.ClosedAt is null)).ShouldBe([
            (SeedIds.Inbox.OpenMention, SeedIds.Users.Tom, SeedIds.Asks.Item, "open", true),
            (SeedIds.Inbox.SeenMention, SeedIds.Users.Tom, SeedIds.Asks.Item, "seen", false),
        ]);
        var comments = await connection.QueryAsync<(Guid Author, string Body)>(
            "SELECT author_id, body FROM comment WHERE id = ANY(@ids)", new { ids = new[] { SeedIds.Inbox.OpenMentionComment, SeedIds.Inbox.SeenMentionComment } });
        comments.ShouldAllBe(c => c.Author == SeedIds.Users.Priya && c.Body.Contains("@Tom Okafor"));
        var stale = await FeatureAsync(SeedIds.Inbox.StaleFeature);
        stale.ShouldBe(("feature", SeedIds.Users.Tom, (DateTimeOffset?)TestDates.Today.AddDays(-5)));

        Task<(string Type, Guid Owner, DateTimeOffset? VerifiedAt)> FeatureAsync(Guid id) =>
            connection.QuerySingleAsync<(string, Guid, DateTimeOffset?)>(
                "SELECT type, coalesce(assigned_to, created_by), status_verified_at FROM work_item WHERE id = @id", new { id });
    }
}
