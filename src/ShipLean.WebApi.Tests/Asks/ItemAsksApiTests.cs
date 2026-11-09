using System.Net;
using Dapper;
using Npgsql;
using ShipLean.Contracts.Asks;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Asks;

// GET api/work-items/{id}/asks through the real app. Joel (project admin) is the default caller, Mei a plain member, Kai in no project.
// Items are built here and given a creator by UPDATE, because WorkItemBuilder never sets created_by.
[TestFixture]
public class ItemAsksApiTests : ApiFixture
{
    private static string Url(Guid id) => $"api/work-items/{id}/asks";

    private static async Task<Guid> ItemAsync(NpgsqlConnection connection, string type, Guid createdBy, Guid? assignee = null, Guid? parent = null)
    {
        var builder = new WorkItemBuilder(SeedIds.Projects.Ship).WithType(type);
        if (assignee is { } a)
        {
            builder.WithAssignee(a);
        }

        if (parent is { } p)
        {
            builder.WithParent(p);
        }

        var id = await builder.InsertAsync(connection);
        await connection.ExecuteAsync("UPDATE work_item SET created_by = @createdBy WHERE id = @id", new { createdBy, id });
        return id;
    }

    [Test]
    public async Task GetAsks_WhenItemHasNone_ShouldReturnEmptyListsAndOwner()
    {
        await using var connection = await OpenDatabaseAsync();
        var item = await ItemAsync(connection, "story", SeedIds.Users.Mei);

        var asks = await Api.GetJsonAsync<ItemAsksDto>(Url(item));

        asks.WorkItemId.ShouldBe(item);
        asks.Questions.ShouldBeEmpty();
        asks.Decisions.ShouldBeEmpty();
        asks.WaitingChildren.ShouldBeEmpty();
        asks.OpenQuestions.ShouldBe(0);
        asks.OpenDecisions.ShouldBe(0);
        asks.Owner.ShouldNotBeNull().Id.ShouldBe(SeedIds.Users.Mei);
    }

    [Test]
    public async Task GetAsks_WhenUnassigned_ShouldNameCreatorAsOwner()
    {
        await using var connection = await OpenDatabaseAsync();
        var item = await ItemAsync(connection, "story", SeedIds.Users.Mei);

        var asks = await Api.GetJsonAsync<ItemAsksDto>(Url(item));

        asks.Owner.ShouldNotBeNull().Id.ShouldBe(SeedIds.Users.Mei);
        asks.OwnerIsCreator.ShouldBeTrue();
        asks.OwnerIsMe.ShouldBeFalse();
    }

    [Test]
    public async Task GetAsks_WhenAssigned_ShouldNameAssigneeNotCreator()
    {
        await using var connection = await OpenDatabaseAsync();
        // Joel (the caller) created it, Mei is assigned: the owner is Mei, so it is not mine.
        var item = await ItemAsync(connection, "story", SeedIds.Users.Joel, assignee: SeedIds.Users.Mei);

        var asks = await Api.GetJsonAsync<ItemAsksDto>(Url(item));

        asks.Owner.ShouldNotBeNull().Id.ShouldBe(SeedIds.Users.Mei);
        asks.OwnerIsCreator.ShouldBeFalse();
        asks.OwnerIsMe.ShouldBeFalse();
    }

    [Test]
    public async Task GetAsks_WhenMixedStates_ShouldCountOnlyOpen()
    {
        await using var connection = await OpenDatabaseAsync();
        var item = await ItemAsync(connection, "story", SeedIds.Users.Mei);
        await new DecisionBuilder(item).BuildAsync(connection);
        await new DecisionBuilder(item).State("withdrawn").BuildAsync(connection);
        await new DecisionBuilder(item).State("confirmed").AnsweredBy(SeedIds.Users.Mei, AskDefaults.Now()).BuildAsync(connection);
        await new QuestionBuilder(item).Withdrawn(SeedIds.Users.Mei, AskDefaults.Now()).BuildAsync(connection);
        await new QuestionBuilder(item).BuildAsync(connection);

        var asks = await Api.GetJsonAsync<ItemAsksDto>(Url(item));

        asks.OpenDecisions.ShouldBe(1);
        asks.OpenQuestions.ShouldBe(1);
        asks.Decisions.Count.ShouldBe(3);
        asks.Decisions.Select(d => d.Label).ShouldBe(["D1", "D2", "D3"]);
        asks.Questions.Select(q => q.Label).ShouldBe(["Q2", "Q1"]);
        asks.Questions.Select(q => q.State).ShouldBe([QuestionState.Open, QuestionState.Withdrawn]);
    }

    [Test]
    public async Task GetAsks_WhenQuestionAnswered_ShouldCarryTheCommentHtml()
    {
        await using var connection = await OpenDatabaseAsync();
        var item = await ItemAsync(connection, "story", SeedIds.Users.Mei);
        var comment = await new CommentBuilder(item).WithAuthor(SeedIds.Users.Mei).WithBody("**Yes**, behind a flag").InsertAsync(connection);
        await new QuestionBuilder(item).Answered(comment, SeedIds.Users.Mei, AskDefaults.Now()).BuildAsync(connection);

        var asks = await Api.GetJsonAsync<ItemAsksDto>(Url(item));

        var question = asks.Questions.Single();
        question.State.ShouldBe(QuestionState.Answered);
        var answer = question.Answer.ShouldNotBeNull();
        answer.CommentId.ShouldBe(comment);
        answer.Body.ShouldBe("**Yes**, behind a flag");
        answer.BodyHtml.ShouldContain("<strong>Yes</strong>");
        answer.By.ShouldNotBeNull().Id.ShouldBe(SeedIds.Users.Mei);
        asks.OpenQuestions.ShouldBe(0);
    }

    [Test]
    public async Task GetAsks_WhenDecisionHasEvents_ShouldListHistoryOldestFirst()
    {
        await using var connection = await OpenDatabaseAsync();
        var item = await ItemAsync(connection, "story", SeedIds.Users.Mei);
        var (decision, _) = await new DecisionBuilder(item).Recommended("A").Alternatives("B")
            .State("changed").Answer("alternative", "B", 0).AnsweredBy(SeedIds.Users.Mei, AskDefaults.Now()).BuildAsync(connection);
        var start = AskDefaults.Now().AddHours(-2);
        // Inserted newest first, so only the ORDER BY can put them right.
        await new AskEventBuilder(item).ForDecision(decision).Kind("changed").From("A").To("B").ToKind("alternative")
            .By(SeedIds.Users.Mei, null).At(start.AddHours(1)).BuildAsync(connection);
        await new AskEventBuilder(item).ForDecision(decision).Kind("raised").To("A").ToKind("recommended").At(start).BuildAsync(connection);

        var asks = await Api.GetJsonAsync<ItemAsksDto>(Url(item));

        var history = asks.Decisions.Single().History;
        history.Select(e => e.Kind).ShouldBe([AskEventKind.Raised, AskEventKind.Changed]);
        history[1].To.ShouldBe("B");
        history[1].ToKind.ShouldBe(AnswerKind.Alternative);
        history[1].By.ShouldNotBeNull().Id.ShouldBe(SeedIds.Users.Mei);
        asks.Decisions.Single().AlternativeIndex.ShouldBe(0);
        asks.Decisions.Single().Alternatives.ShouldBe(["B"]);
    }

    [Test]
    public async Task GetAsks_WhenFeatureHasWaitingStory_ShouldListOnlyChildrenWithOpenAsks()
    {
        await using var connection = await OpenDatabaseAsync();
        var feature = await ItemAsync(connection, "feature", SeedIds.Users.Joel);
        var waiting = await ItemAsync(connection, "story", SeedIds.Users.Mei, parent: feature);
        var quiet = await ItemAsync(connection, "story", SeedIds.Users.Mei, parent: feature);
        var settled = await ItemAsync(connection, "bug", SeedIds.Users.Mei, parent: feature);
        await new QuestionBuilder(waiting).BuildAsync(connection);
        await new DecisionBuilder(waiting).BuildAsync(connection);
        await new DecisionBuilder(waiting).BuildAsync(connection);
        await new DecisionBuilder(settled).State("withdrawn").BuildAsync(connection);

        var asks = await Api.GetJsonAsync<ItemAsksDto>(Url(feature));

        var child = asks.WaitingChildren.ShouldHaveSingleItem();
        child.Id.ShouldBe(waiting);
        child.Type.ShouldBe("story");
        child.OpenQuestions.ShouldBe(1);
        child.OpenDecisions.ShouldBe(2);
        child.OwnerIsCreator.ShouldBeTrue();
        child.Owner.ShouldNotBeNull().Id.ShouldBe(SeedIds.Users.Mei);
        asks.WaitingChildren.ShouldNotContain(c => c.Id == quiet || c.Id == settled);
        (await Api.GetJsonAsync<ItemAsksDto>(Url(waiting))).WaitingChildren.ShouldBeEmpty();
    }

    [Test]
    public async Task GetAsks_WhenFeatureHasActionChildWithAsks_ShouldNotListIt()
    {
        await using var connection = await OpenDatabaseAsync();
        var feature = await ItemAsync(connection, "feature", SeedIds.Users.Joel);
        var action = await ItemAsync(connection, "action", SeedIds.Users.Mei, parent: feature);
        await new QuestionBuilder(action).BuildAsync(connection);

        var asks = await Api.GetJsonAsync<ItemAsksDto>(Url(feature));

        asks.WaitingChildren.ShouldBeEmpty();
    }

    [Test]
    public async Task GetAsks_WhenNotMember_ShouldBeNotFound()
    {
        using var response = await Api.ActingAs(TestUser.Kai).GetAsync(Url(SeedIds.Asks.Item));

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound);
        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }
}
