using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Npgsql;
using ShipLean.Contracts.Asks;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Asks;

// The ask lines of GET api/work-items/{id}/history. Asks are arranged with the builders only, each decision with its raised
// event, because the writers are not what is under test. Joel is the default caller.
[TestFixture]
public class AskHistoryApiTests : ApiFixture
{
    private const string Agent = "Claude Code (test)";

    private static readonly JsonSerializerOptions WireOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private async Task<HistoryDto> ReadHistoryAsync(Guid id)
    {
        using var response = await Api.GetAsync($"api/work-items/{id}/history");
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonSerializer.Deserialize<HistoryDto>(text, WireOptions)!;
    }

    private async Task<Guid> ArrangeItemAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        return await new WorkItemBuilder(SeedIds.Projects.Ship).InsertAsync(connection);
    }

    private static async Task<Guid> RaisedAsync(NpgsqlConnection connection, Guid item, DateTimeOffset at, string agent = Agent)
    {
        var (id, _) = await new DecisionBuilder(item).RaisedBy(SeedIds.Users.Joel, agent, at).BuildAsync(connection);
        await new AskEventBuilder(item).ForDecision(id).Kind("raised").To("The recommended answer").ToKind("recommended")
            .By(SeedIds.Users.Joel, agent).At(at).BuildAsync(connection);
        return id;
    }

    [Test]
    public async Task History_WhenDecisionChanged_ShouldListAskLineWithFromAndTo()
    {
        var item = await ArrangeItemAsync();
        var at = TestDates.Today.AddHours(1);
        await using (var connection = await OpenDatabaseAsync())
        {
            var decision = await RaisedAsync(connection, item, at);
            await new AskEventBuilder(item).ForDecision(decision).Kind("changed").From("The recommended answer").To("Another way")
                .ToKind("own").Note("Because.").By(SeedIds.Users.Mei, null).At(at.AddHours(1)).BuildAsync(connection);
        }

        var asks = (await ReadHistoryAsync(item)).Asks;

        asks.Select(a => a.Kind).ShouldBe([AskEventKind.Changed, AskEventKind.Raised]);
        var changed = asks[0];
        changed.Badge.ShouldBe("D1");
        changed.From.ShouldBe("The recommended answer");
        changed.To.ShouldBe("Another way");
        changed.Note.ShouldBe("Because.");
        changed.By.ShouldNotBeNull().Id.ShouldBe(SeedIds.Users.Mei);
        changed.AtLabel.ShouldNotBeNullOrWhiteSpace();
        asks[1].To.ShouldBe("The recommended answer");
        asks[1].AgentName.ShouldBe(Agent);
    }

    [Test]
    public async Task History_WhenFiveRaisedTogether_ShouldFoldToOneLine()
    {
        var item = await ArrangeItemAsync();
        var at = TestDates.Today.AddHours(2);
        await using (var connection = await OpenDatabaseAsync())
        {
            for (var n = 0; n < 5; n++)
            {
                await RaisedAsync(connection, item, at);
            }
        }

        var asks = (await ReadHistoryAsync(item)).Asks;

        var line = asks.ShouldHaveSingleItem();
        line.Badge.ShouldBe("D1–D5");
        line.Kind.ShouldBe(AskEventKind.Raised);
        line.AgentName.ShouldBe(Agent);
        line.At.ShouldBe(at);
    }

    [Test]
    public async Task History_WhenRaisedAtDifferentTimes_ShouldNotFold()
    {
        var item = await ArrangeItemAsync();
        var at = TestDates.Today.AddHours(3);
        await using (var connection = await OpenDatabaseAsync())
        {
            await RaisedAsync(connection, item, at);
            await RaisedAsync(connection, item, at.AddMinutes(1));
            await RaisedAsync(connection, item, at.AddMinutes(1), agent: "Another agent");
        }

        var asks = (await ReadHistoryAsync(item)).Asks;

        asks.Select(a => a.Badge).ShouldBe(["D3", "D2", "D1"]);
    }

    [Test]
    public async Task History_WhenQuestionAnswered_ShouldListAskedAndAnswered()
    {
        var item = await ArrangeItemAsync();
        var at = TestDates.Today.AddHours(4);
        await using (var connection = await OpenDatabaseAsync())
        {
            var (question, _) = await new QuestionBuilder(item).AskedBy(SeedIds.Users.Joel, Agent, at).BuildAsync(connection);
            await new AskEventBuilder(item).ForQuestion(question).Kind("asked").By(SeedIds.Users.Joel, Agent).At(at).BuildAsync(connection);
            await new AskEventBuilder(item).ForQuestion(question).Kind("answered").By(SeedIds.Users.Mei, null).At(at.AddHours(1)).BuildAsync(connection);
        }

        var asks = (await ReadHistoryAsync(item)).Asks;

        asks.Select(a => (a.Kind, a.Badge)).ShouldBe([(AskEventKind.Answered, "Q1"), (AskEventKind.Asked, "Q1")]);
        asks[0].By.ShouldNotBeNull().Id.ShouldBe(SeedIds.Users.Mei);
        asks[0].To.ShouldBeNull();
    }

    [Test]
    public async Task History_WhenNoAsks_ShouldReturnEmptyAsksAndSameRevisions()
    {
        var item = await ArrangeItemAsync();

        var history = await ReadHistoryAsync(item);

        history.Asks.ShouldBeEmpty();
        history.Revisions.Select(r => r.Rev).ShouldBe([1]);
    }
}
