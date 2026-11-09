using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Npgsql;
using ShipLean.Contracts.Asks;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Asks;

// GET api/me/waiting through the real app. Items are built here and given a creator by UPDATE, because WorkItemBuilder never
// sets created_by. The database is shared by the class, so each test asserts on the items it built. Priya is used only by
// the settled test, so nothing else gives her open asks.
[TestFixture]
public class WaitingApiTests : ApiFixture
{
    private static readonly JsonSerializerOptions WireOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private async Task<WaitingDto> WaitingAsync(TestUser? user = null)
    {
        using var response = await (user is null ? Api : Api.ActingAs(user)).GetAsync("api/me/waiting");
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonSerializer.Deserialize<WaitingDto>(text, WireOptions)!;
    }

    private static async Task<Guid> ItemAsync(
        NpgsqlConnection connection, Guid project, Guid createdBy, Guid? assignee = null, string? state = null)
    {
        var builder = new WorkItemBuilder(project);
        if (assignee is { } a)
        {
            builder.WithAssignee(a);
        }

        if (state is not null)
        {
            builder.WithStateNamed(state);
        }

        var id = await builder.InsertAsync(connection);
        await connection.ExecuteAsync("UPDATE work_item SET created_by = @createdBy WHERE id = @id", new { createdBy, id });
        return id;
    }

    private static async Task OpenAsksAsync(NpgsqlConnection connection, Guid item, int decisions = 1, int questions = 1, DateTimeOffset? at = null)
    {
        var when = at ?? TestDates.Today.AddHours(1);
        for (var n = 0; n < decisions; n++)
        {
            await new DecisionBuilder(item).RaisedBy(SeedIds.Users.Tom, null, when).BuildAsync(connection);
        }

        for (var n = 0; n < questions; n++)
        {
            await new QuestionBuilder(item).AskedBy(SeedIds.Users.Tom, null, when).BuildAsync(connection);
        }
    }

    [Test]
    public async Task Waiting_WhenIAmAssignee_ShouldListItem()
    {
        await using var connection = await OpenDatabaseAsync();
        var item = await ItemAsync(connection, SeedIds.Projects.Ship, SeedIds.Users.Mei, assignee: SeedIds.Users.Joel);
        await OpenAsksAsync(connection, item, decisions: 2, questions: 1);

        var waiting = await WaitingAsync();

        var listed = waiting.Items.Single(i => i.Id == item);
        listed.OpenDecisions.ShouldBe(2);
        listed.OpenQuestions.ShouldBe(1);
        listed.ProjectId.ShouldBe(SeedIds.Projects.Ship);
        listed.ProjectKey.ShouldBe("SHIP");
        listed.OldestAtLabel.ShouldNotBeNullOrWhiteSpace();
        waiting.OpenDecisions.ShouldBe(waiting.Items.Sum(i => i.OpenDecisions));
        waiting.OpenQuestions.ShouldBe(waiting.Items.Sum(i => i.OpenQuestions));
    }

    [Test]
    public async Task Waiting_WhenAssignedToOtherButCreatedByMe_ShouldNotListItem()
    {
        await using var connection = await OpenDatabaseAsync();
        var item = await ItemAsync(connection, SeedIds.Projects.Ship, SeedIds.Users.Joel, assignee: SeedIds.Users.Mei);
        await OpenAsksAsync(connection, item);

        var waiting = await WaitingAsync();

        waiting.Items.ShouldNotContain(i => i.Id == item);
    }

    [Test]
    public async Task Waiting_WhenUnassignedAndCreatedByMe_ShouldListItem()
    {
        await using var connection = await OpenDatabaseAsync();
        var item = await ItemAsync(connection, SeedIds.Projects.Ship, SeedIds.Users.Joel);
        await OpenAsksAsync(connection, item);

        var waiting = await WaitingAsync();

        waiting.Items.ShouldContain(i => i.Id == item);
    }

    [Test]
    public async Task Waiting_WhenTwoProjects_ShouldListBoth()
    {
        await using var connection = await OpenDatabaseAsync();
        var newer = await ItemAsync(connection, SeedIds.Projects.Ship, SeedIds.Users.Joel);
        var older = await ItemAsync(connection, SeedIds.Projects.Pay, SeedIds.Users.Joel);
        await OpenAsksAsync(connection, newer, at: TestDates.Today.AddHours(5));
        await OpenAsksAsync(connection, older, at: TestDates.Today.AddHours(2));

        var ids = (await WaitingAsync()).Items.Select(i => i.Id).ToList();

        ids.ShouldContain(newer);
        ids.ShouldContain(older);
        ids.IndexOf(older).ShouldBeLessThan(ids.IndexOf(newer));
    }

    [Test]
    public async Task Waiting_WhenNoLongerMember_ShouldNotListThatProject()
    {
        await using var connection = await OpenDatabaseAsync();
        var kept = await ItemAsync(connection, SeedIds.Projects.Ship, SeedIds.Users.Mei);
        var left = await ItemAsync(connection, SeedIds.Projects.Mob, SeedIds.Users.Mei);
        await OpenAsksAsync(connection, kept);
        await OpenAsksAsync(connection, left);
        await connection.ExecuteAsync(
            "UPDATE project_member SET archived_at = now() WHERE project_id = @project AND user_id = @user",
            new { project = SeedIds.Projects.Mob, user = SeedIds.Users.Mei });

        var ids = (await WaitingAsync(TestUser.Mei)).Items.Select(i => i.Id).ToList();

        ids.ShouldContain(kept);
        ids.ShouldNotContain(left);
    }

    [Test]
    public async Task Waiting_WhenItemRemoved_ShouldNotListIt()
    {
        await using var connection = await OpenDatabaseAsync();
        var item = await ItemAsync(connection, SeedIds.Projects.Ship, SeedIds.Users.Joel, state: "Removed");
        await OpenAsksAsync(connection, item);

        var waiting = await WaitingAsync();

        waiting.Items.ShouldNotContain(i => i.Id == item);
    }

    [Test]
    public async Task Waiting_WhenOnlySettled_ShouldBeEmpty()
    {
        await using var connection = await OpenDatabaseAsync();
        var item = await ItemAsync(connection, SeedIds.Projects.Ship, SeedIds.Users.Priya);
        await new DecisionBuilder(item).State("withdrawn").BuildAsync(connection);
        await new DecisionBuilder(item).State("confirmed").BuildAsync(connection);
        await new QuestionBuilder(item).Withdrawn(SeedIds.Users.Priya, TestDates.Today).BuildAsync(connection);

        var waiting = await WaitingAsync(TestUser.Priya);

        waiting.Items.ShouldBeEmpty();
        waiting.OpenDecisions.ShouldBe(0);
        waiting.OpenQuestions.ShouldBe(0);
    }

    [Test]
    public async Task Waiting_WhenSignedOut_ShouldBe401()
    {
        using var response = await Api.ActingAs(TestUser.Anonymous).GetAsync("api/me/waiting");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Unauthorized)).Code.ShouldBe(ApiErrorCodes.Unauthorized);
    }

    [Test]
    public async Task Waiting_WhenSeededAsksExist_ShouldListTomsStoryForTom()
    {
        var waiting = await WaitingAsync(TestUser.Tom);

        var story = waiting.Items.Single(i => i.Id == SeedIds.Asks.Item);
        story.Key.ShouldBe("SHIP-124");
        story.OpenDecisions.ShouldBe(4);
        story.OpenQuestions.ShouldBe(2);
    }
}
