using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using ShipLean.Contracts;
using ShipLean.Contracts.Inbox;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.DeviceSessions;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Inbox;

// The Inbox end to end as the phone app drives it: a device bearer for each person, the web routes for what a person does,
// and /mcp as the agent that raises the asks. Nothing is written by a builder except the fresh people and the Feature and
// Story they own (and the status line's age); every ask, answer and comment goes through its route or tool.
[TestFixture]
[CancelAfter(120000)]
public class InboxJourneyTests : McpFixture
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    protected override bool WithProbeTools => false;

    // Only a bearer signs anyone in, as on the phone.
    protected override bool UseTestSignIn => false;

    private sealed record Arranged(Guid Owner, string OwnerName, Guid Feature, string FeatureKey, Guid Story, string StoryKey);

    // A fresh owner in SHIP, their Feature with a status line last checked statusAge ago, and a Story under it.
    private async Task<Arranged> ArrangeAsync(string ownerName, TimeSpan statusAge)
    {
        await using var connection = await OpenDatabaseAsync();
        var owner = await new AppUserBuilder().WithName(ownerName).InsertAsync(connection);
        await connection.ExecuteAsync(
            "INSERT INTO project_member (id, project_id, user_id, role) VALUES (@id, @project, @owner, 'member')",
            new { id = Guid.NewGuid(), project = SeedIds.Projects.Ship, owner });
        var checkedAt = TimeProvider.System.GetUtcNow() - statusAge;
        var feature = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithTitle("Journey Feature").WithAssignee(owner)
            .WithStatusLine("Going well.", checkedAt, owner, null).InsertAsync(connection);
        var story = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithTitle("Journey Story").WithAssignee(owner)
            .WithParent(feature).InsertAsync(connection);
        return new Arranged(
            owner, ownerName, feature, await KeyAsync(connection, feature), story, await KeyAsync(connection, story));
    }

    private static Task<string> KeyAsync(Npgsql.NpgsqlConnection connection, Guid id)
        => connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @id", new { id });

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, HttpStatusCode status = HttpStatusCode.OK)
    {
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(status, text);
        return JsonSerializer.Deserialize<T>(text, Wire)!;
    }

    private static async Task<InboxDto> InboxAsync(ApiClient phone)
    {
        using var response = await phone.GetAsync($"api/projects/{SeedIds.Projects.Ship}/inbox");
        return await ReadAsync<InboxDto>(response);
    }

    private static async Task<AnsweredDto> AnsweredAsync(ApiClient phone)
    {
        using var response = await phone.GetAsync($"api/projects/{SeedIds.Projects.Ship}/inbox/answered");
        return await ReadAsync<AnsweredDto>(response);
    }

    private static async Task NoContentAsync(Task<HttpResponseMessage> request)
    {
        using var response = await request;
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
    }

    private async Task<ApiClient> PhoneAsync(Guid userId)
    {
        var session = await DeviceBearerClient.CreateAsync(Host, userId);
        return new ApiClient(session.Client);
    }

    private async Task AgentAsync(string token, string tool, string arguments)
    {
        var reply = await CallToolAsync(token, tool, arguments);
        reply.IsToolError().ShouldBeFalse(reply.Body);
    }

    [Test]
    public async Task InboxJourney_WhenEveryKindRaisedAndAnswered_ShouldEmptyInboxAndListAnswered()
    {
        var a = await ArrangeAsync("Olive Ownerson", TimeSpan.FromDays(4));
        var owner = await PhoneAsync(a.Owner);
        var priya = await PhoneAsync(SeedIds.Users.Priya);
        var agent = await TokenAsync(TestUser.Of(a.Owner), ReadWrite);

        // 1. An agent raises D1-D3 on the Feature and Q1 with two options on the Story; Priya mentions the owner on the web.
        await AgentAsync(agent, "record_decisions",
            $$"""{"item":"{{a.FeatureKey}}","decisions":[{"question":"First?","recommended":"Yes","alternatives":["No"]},{"question":"Second?","recommended":"Yes","alternatives":["No"]},{"question":"Third?","recommended":"Yes","alternatives":["Later"]}]}""");
        await AgentAsync(agent, "ask", $$"""{"item":"{{a.StoryKey}}","text":"Which card?","options":["Visa","Mastercard"]}""");
        using (var mention = await priya.PostJsonAsync($"api/work-items/{a.Feature}/comments", new { body = $"@{a.OwnerName} can you check?" }))
        {
            mention.StatusCode.ShouldBe(HttpStatusCode.OK, await mention.Content.ReadAsStringAsync());
        }

        // 2. The Inbox: the Feature's row, then the Story's under it; six asks, the question's options, the mention as @1.
        var inbox = await InboxAsync(owner);
        inbox.Items.Select(i => i.Key).ShouldBe([a.FeatureKey, a.StoryKey]);
        inbox.OpenAsks.ShouldBe(6);
        inbox.OpenItems.ShouldBe(2);
        inbox.Items[0].UnderKey.ShouldBeNull();
        inbox.Items[1].UnderKey.ShouldBe(a.FeatureKey);
        inbox.Items[0].Asks.Select(k => k.Label).ShouldBe(["D1", "D2", "D3", "@1", "Status"]);
        inbox.Items[1].Asks.Single().Question.ShouldNotBeNull().Options.ShouldBe(["Visa", "Mastercard"]);
        var mentionAsk = inbox.Items[0].Asks.Single(k => k.Kind == InboxAskKind.Mention);
        mentionAsk.Mention.ShouldNotBeNull().Excerpt.ShouldContain("can you check?");
        mentionAsk.From.ShouldNotBeNull().Id.ShouldBe(SeedIds.Users.Priya);

        // 3. The owner answers everything.
        await NoContentAsync(owner.PostJsonAsync($"api/work-items/{a.Feature}/decisions/confirm", new { numbers = new[] { 1, 2 } }));
        await NoContentAsync(owner.PostJsonAsync($"api/work-items/{a.Feature}/decisions/3/change", new { kind = "alternative", alternativeIndex = 0 }));
        await NoContentAsync(owner.PostJsonAsync($"api/work-items/{a.Story}/questions/1/answer", new { body = "Visa" }));
        await NoContentAsync(owner.PostJsonAsync($"api/mentions/{mentionAsk.Mention.MentionId}/reply", new { body = "On it." }));
        var beforeStill = await InboxAsync(owner);
        var statusRow = beforeStill.Items.Single(i => i.Key == a.FeatureKey);
        statusRow.Asks.Single().Kind.ShouldBe(InboxAskKind.Status);
        using (var still = await owner.PostJsonAsync($"api/work-items/{a.Feature}/status/confirm", new { revision = statusRow.Revision }))
        {
            (await ReadAsync<WorkItemDto>(still)).StatusVerified.ShouldNotBeNull();
        }

        // 4. Nothing is waiting, and something once was.
        var emptied = await InboxAsync(owner);
        emptied.Items.ShouldBeEmpty();
        emptied.OpenAsks.ShouldBe(0);
        emptied.EverWaited.ShouldBeTrue();

        // 5. An agent picks the Feature up; Answered lists each answer, and Priya now has the reply to read.
        await AgentAsync(agent, "comment", $$"""{"item":"{{a.FeatureKey}}","text":"Picked up, building D1 and D2."}""");
        var answered = await AnsweredAsync(owner);
        var rows = answered.Rows.ToDictionary(r => r.Summary);
        rows.Keys.ShouldBe(["Approved D1–D2", "Changed D3", "Answered Q1", "Replied to Priya Shah"], ignoreOrder: true);
        rows["Approved D1–D2"].Next.ShouldNotBeNull().Kind.ShouldBe(AnsweredNextKind.AgentPickedUp);
        rows["Changed D3"].Key.ShouldBe(a.FeatureKey);
        rows["Answered Q1"].Key.ShouldBe(a.StoryKey);
        rows["Answered Q1"].Next.ShouldBeNull();
        var priyaInbox = await InboxAsync(priya);
        var reply = priyaInbox.Items.Single(i => i.Key == a.FeatureKey).Asks.Single();
        reply.Kind.ShouldBe(InboxAskKind.Mention);
        reply.Mention.ShouldNotBeNull().Excerpt.ShouldContain("On it.");
    }

    [Test]
    public async Task InboxJourney_WhenSignedInAsSeededTom_ShouldListSeededAsksOnly()
    {
        var tom = await PhoneAsync(SeedIds.Users.Tom);
        var priya = await PhoneAsync(SeedIds.Users.Priya);

        var inbox = await InboxAsync(tom);

        var asks = inbox.Items.Single(i => i.Key == "SHIP-124");
        asks.Asks.Where(k => k.Kind == InboxAskKind.Decision).Select(k => k.Label).ShouldBe(["D1", "D2", "D3", "D4"]);
        asks.Asks.Where(k => k.Kind == InboxAskKind.Question).Select(k => k.Label).ShouldBe(["Q1", "Q2"]);
        asks.Asks.Single(k => k.Kind == InboxAskKind.Mention).Mention.ShouldNotBeNull().MentionId.ShouldBe(SeedIds.Inbox.OpenMention);
        asks.Asks.Select(k => k.Mention?.MentionId).ShouldNotContain(SeedIds.Inbox.SeenMention);
        var stale = inbox.Items.Single(i => i.Key == "SHIP-103");
        stale.Asks.Single().Kind.ShouldBe(InboxAskKind.Status);
        inbox.Items.Select(i => i.Key).ShouldNotContain("SHIP-104");
        inbox.Items.Select(i => i.Key).ShouldBe(["SHIP-124", "SHIP-103"], ignoreOrder: true);
        // Nothing seeded waits on Priya (the journey above may have left its own reply for her in this scenario's database).
        (await InboxAsync(priya)).Items.Select(i => i.Key).ShouldNotContain(k => k == "SHIP-124" || k == "SHIP-103" || k == "SHIP-104");
    }

    [Test]
    public async Task InboxJourney_WhenStillTrueSentWithOldRevision_ShouldReturnConflict()
    {
        var a = await ArrangeAsync("Orla Ownerston", TimeSpan.FromDays(4));
        var owner = await PhoneAsync(a.Owner);
        var revision = (await InboxAsync(owner)).Items.Single(i => i.Key == a.FeatureKey).Revision;
        using (var first = await owner.PostJsonAsync($"api/work-items/{a.Feature}/status/confirm", new { revision }))
        {
            (await ReadAsync<WorkItemDto>(first)).Revision.ShouldBe(revision + 1);
        }

        using var stale = await owner.PostJsonAsync($"api/work-items/{a.Feature}/status/confirm", new { revision });

        var conflict = await ReadAsync<ConflictDto>(stale, HttpStatusCode.Conflict);
        conflict.Current.Revision.ShouldBe(revision + 1);
    }
}
