using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Search;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Search;

namespace ShipLean.WebApi.Tests.Search.Journeys;

// Writer, pass, ranker and GET /api/search end to end. Every write goes through the real API; every search follows a full reindex
// (no background service runs in tests), with the host's FakeEmbedder. Each test uses words nothing else in the seed contains.
[TestFixture]
public class SearchJourneyTests : ApiFixture
{
    private FakeEmbedder Embedder => (FakeEmbedder)Host.Services.GetRequiredService<IEmbedder>();

    [Test]
    public async Task Index_WhenTitleChanged_ShouldReembedOnNextPass()
    {
        var item = await JourneyApi.CreateItemAsync(Api, SeedIds.Projects.Ship, "Marmot burrow inventory");
        await JourneyApi.ReindexAsync(Api);
        var first = await TitleRowAsync(item.Id);
        JourneyApi.ItemKeys(await JourneyApi.SearchAsync(Api, "marmot burrow")).ShouldContain(item.Key);
        var embeddedBefore = Embedder.PassageTexts;

        await JourneyApi.RetitleAsync(Api, item, "Walrus tusk calibration");
        await JourneyApi.ReindexAsync(Api);

        var second = await TitleRowAsync(item.Id);
        first.Text.ShouldBe("Marmot burrow inventory");
        first.Embedded.ShouldBeTrue();
        second.Text.ShouldBe("Walrus tusk calibration");
        second.Embedded.ShouldBeTrue();
        second.Hash.ShouldNotBe(first.Hash);
        (Embedder.PassageTexts - embeddedBefore).ShouldBe(1);
        JourneyApi.ItemKeys(await JourneyApi.SearchAsync(Api, "walrus tusk")).ShouldContain(item.Key);
        JourneyApi.ItemKeys(await JourneyApi.SearchAsync(Api, "marmot burrow")).ShouldNotContain(item.Key);
    }

    [Test]
    public async Task SearchJourney_WhenCommentAdded_ShouldFindItemWithCommentSnippet()
    {
        var item = await JourneyApi.CreateItemAsync(Api, SeedIds.Projects.Ship, "Ferry timetable review");
        await JourneyApi.CommentAsync(Api, item.Id, "The pangolin handshake fails on the late crossing.");
        await JourneyApi.ReindexAsync(Api);

        var search = await JourneyApi.SearchAsync(Api, "pangolin handshake");

        var hit = search.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("key").GetString() == item.Key);
        hit.GetProperty("matchedIn").GetString().ShouldBe("comment");
        hit.GetProperty("snippet").GetString()!.ShouldContain("pangolin");
        hit.GetProperty("title").GetString().ShouldBe("Ferry timetable review");
    }

    [Test]
    public async Task SearchJourney_WhenItemRemoved_ShouldDisappear()
    {
        var item = await JourneyApi.CreateItemAsync(Api, SeedIds.Projects.Ship, "Lemur census backlog");
        await JourneyApi.CommentAsync(Api, item.Id, "The lemur census needs a second counter.");
        await JourneyApi.ReindexAsync(Api);
        JourneyApi.ItemKeys(await JourneyApi.SearchAsync(Api, "lemur census")).ShouldContain(item.Key);
        JourneyApi.ItemKeys(await JourneyApi.SearchAsync(Api, "second counter")).ShouldContain(item.Key);

        await JourneyApi.RemoveAsync(Api, item);
        await JourneyApi.ReindexAsync(Api);

        JourneyApi.ItemKeys(await JourneyApi.SearchAsync(Api, "lemur census")).ShouldNotContain(item.Key);
        JourneyApi.ItemKeys(await JourneyApi.SearchAsync(Api, "second counter")).ShouldNotContain(item.Key);
    }

    [Test]
    public async Task SearchJourney_WhenSprintGoalSet_ShouldFindSprint()
    {
        await JourneyApi.CreateSprintAsync(Api, SeedIds.Projects.Ship, "Sprint Heron", "Stabilise the quagga export", 800);
        await JourneyApi.ReindexAsync(Api);

        var search = await JourneyApi.SearchAsync(Api, "quagga export");

        var sprint = search.GetProperty("sprints").EnumerateArray().Single(s => s.GetProperty("name").GetString() == "Sprint Heron");
        sprint.GetProperty("name").GetString().ShouldBe("Sprint Heron");
        sprint.GetProperty("goal").GetString().ShouldBe("Stabilise the quagga export");
        sprint.GetProperty("projectKey").GetString().ShouldBe("SHIP");
    }

    private async Task<(string Text, string Hash, bool Embedded)> TitleRowAsync(Guid itemId)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.QuerySingleAsync<(string, string, bool)>(
            "SELECT text, text_hash, embedding IS NOT NULL FROM search_doc WHERE source_kind = 'item' AND source_id = @itemId AND field = 'title'", new { itemId });
    }
}
