using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Application.Markdown;
using ShipLean.Contracts;
using ShipLean.Contracts.Asks;
using ShipLean.Contracts.Comments;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Asks;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Contracts.Mcp;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Asks;

// Questions (SV-Q) through the real app and database. AskAsync has no route, so the Ask tests resolve IQuestionWrites from a scope
// acting as Joel; answer and withdraw go over REST. Joel is the default caller, Mei a plain member, Kai in no project. State is
// asserted through AskRows, field by field.
[TestFixture]
public class QuestionApiTests : ApiFixture
{
    private static readonly JsonSerializerOptions WireOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly SettableCurrentAgent _agent = new();

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.RemoveAll<ICurrentAgent>();
        services.AddSingleton<ICurrentAgent>(_agent);
    };

    [SetUp]
    public void NoAgentByDefault() => _agent.Clear();

    [Test]
    public async Task Ask_WhenTwoAsked_ShouldNumberQ1AndQ2()
    {
        var item = await NewItemAsync();

        var first = await AskAsync(item, "Which card network?");
        var second = await AskAsync(item, "Which wallet?");

        (first.Number, first.Label, second.Number, second.Label).ShouldBe((1, "Q1", 2, "Q2"));
        await using var connection = await OpenDatabaseAsync();
        (await AskRows.QuestionsAsync(connection, item)).Select(q => (q.Number, q.Text, q.State, q.CreatedByUserId))
            .ShouldBe([(1, "Which card network?", "open", SeedIds.Users.Joel), (2, "Which wallet?", "open", SeedIds.Users.Joel)]);
        (await AskRows.EventsAsync(connection, item)).Select(e => e.Kind).ShouldBe(["asked", "asked"]);
    }

    [Test]
    public async Task Ask_WhenEarlierWithdrawn_ShouldNotReuseNumber()
    {
        var item = await NewItemAsync();
        await using var connection = await OpenDatabaseAsync();
        await new QuestionBuilder(item).Number(1).BuildAsync(connection);
        await new QuestionBuilder(item).Number(2).Withdrawn(SeedIds.Users.Joel, AskDefaults.Now()).BuildAsync(connection);

        var asked = await AskAsync(item, "Next?");

        asked.Number.ShouldBe(3);
    }

    [Test]
    public async Task Ask_WhenAgentActs_ShouldStampAgentName()
    {
        var item = await NewItemAsync();
        _agent.ActAs("Claude Code", "claude.ai", true);

        await AskAsync(item, "Stamped?");

        await using var connection = await OpenDatabaseAsync();
        var question = (await AskRows.QuestionsAsync(connection, item)).ShouldHaveSingleItem();
        question.CreatedAgentName.ShouldBe("Claude Code (claude.ai)");
        (await AskRows.EventsAsync(connection, item)).ShouldHaveSingleItem().AgentName.ShouldBe("Claude Code (claude.ai)");
    }

    [Test]
    public async Task Answer_WhenOpen_ShouldPostTaggedCommentAndMarkAnswered()
    {
        var (item, question) = await OpenQuestionAsync();

        using var response = await Api.PostJsonAsync(AnswerUrl(item, 1), new AnswerQuestionRequest("Use Visa."));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await using var connection = await OpenDatabaseAsync();
        var stored = await AskRows.QuestionAsync(connection, question);
        (stored.State, stored.AnsweredByUserId).ShouldBe(("answered", SeedIds.Users.Joel));
        stored.AnsweredAt.ShouldNotBeNull();
        var comments = await CommentsAsync(Api, item);
        var comment = comments.ShouldHaveSingleItem();
        comment.Body.ShouldBe("Use Visa.");
        (comment.AskTag!.Kind, comment.AskTag.Label).ShouldBe((AskTagKind.Answer, "Q1"));
        stored.AnswerCommentId.ShouldBe(comment.Id);
        var tag = (await AskRows.TagAsync(connection, comment.Id)).ShouldNotBeNull();
        (tag.Kind, tag.QuestionId).ShouldBe(("answer", question));
        (await AskRows.EventsAsync(connection, item)).Select(e => (e.QuestionId, e.Kind, e.ByUserId)).ShouldBe([(question, "answered", SeedIds.Users.Joel)]);
    }

    [Test]
    public async Task Answer_WhenPersonOnWeb_ShouldPostCommentWithoutAgentName()
    {
        var (item, _) = await OpenQuestionAsync();

        using var response = await Api.PostJsonAsync(AnswerUrl(item, 1), new AnswerQuestionRequest("By hand."));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await CommentsAsync(Api, item)).ShouldHaveSingleItem().AgentName.ShouldBeNull();
        await using var connection = await OpenDatabaseAsync();
        (await AskRows.EventsAsync(connection, item)).ShouldHaveSingleItem().AgentName.ShouldBeNull();
    }

    [Test]
    public async Task Answer_WhenMemberIsNotOwner_ShouldAnswer()
    {
        var (item, question) = await OpenQuestionAsync();
        var mei = Api.ActingAs(TestUser.Mei);

        using var response = await mei.PostJsonAsync(AnswerUrl(item, 1), new AnswerQuestionRequest("Mei says Visa."));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await using var connection = await OpenDatabaseAsync();
        var stored = await AskRows.QuestionAsync(connection, question);
        (stored.State, stored.AnsweredByUserId).ShouldBe(("answered", SeedIds.Users.Mei));
    }

    [Test]
    public async Task Answer_WhenAlreadyAnswered_ShouldBeConflict()
    {
        var (item, question) = await OpenQuestionAsync();
        using var first = await Api.PostJsonAsync(AnswerUrl(item, 1), new AnswerQuestionRequest("First."));
        first.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var second = await Api.PostJsonAsync(AnswerUrl(item, 1), new AnswerQuestionRequest("Second."));

        (await ApiClient.ReadApiErrorAsync(second, HttpStatusCode.Conflict)).Code.ShouldBe(ApiErrorCodes.Conflict);
        (await CommentsAsync(Api, item)).ShouldHaveSingleItem().Body.ShouldBe("First.");
        await using var connection = await OpenDatabaseAsync();
        (await AskRows.EventsAsync(connection, item)).Count(e => e.QuestionId == question).ShouldBe(1);
    }

    [Test]
    public async Task Answer_WhenBodyBlank_ShouldFailValidationOnBody()
    {
        var (item, question) = await OpenQuestionAsync();

        using var response = await Api.PostJsonAsync(AnswerUrl(item, 1), new AnswerQuestionRequest("  "));

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        (error.Code, error.Field).ShouldBe((ApiErrorCodes.Validation, "body"));
        await using var connection = await OpenDatabaseAsync();
        (await AskRows.QuestionAsync(connection, question)).State.ShouldBe("open");
    }

    [Test]
    public async Task Answer_WhenCommentFails_ShouldLeaveQuestionOpen()
    {
        var (item, question) = await OpenQuestionAsync();

        using var response = await Api.PostJsonAsync(AnswerUrl(item, 1), new AnswerQuestionRequest(new string('x', MarkdownRenderer.MaxLength + 1)));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
        await using var connection = await OpenDatabaseAsync();
        var stored = await AskRows.QuestionAsync(connection, question);
        (stored.State, stored.AnswerCommentId).ShouldBe(("open", null));
        (await AskRows.TagsAsync(connection, item)).ShouldBeEmpty();
        (await AskRows.EventsAsync(connection, item)).ShouldBeEmpty();
        (await CommentsAsync(Api, item)).ShouldBeEmpty();
    }

    [Test]
    public async Task Withdraw_WhenOpen_ShouldWithdraw()
    {
        var (item, question) = await OpenQuestionAsync();

        using var response = await Api.PostJsonAsync(WithdrawUrl(item, 1));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await using var connection = await OpenDatabaseAsync();
        var stored = await AskRows.QuestionAsync(connection, question);
        (stored.State, stored.WithdrawnByUserId).ShouldBe(("withdrawn", SeedIds.Users.Joel));
        stored.WithdrawnAt.ShouldNotBeNull();
        (await AskRows.EventsAsync(connection, item)).Select(e => (e.QuestionId, e.Kind)).ShouldBe([(question, "withdrawn")]);
    }

    [Test]
    public async Task Withdraw_WhenAnswered_ShouldBeConflict()
    {
        var (item, question) = await OpenQuestionAsync();
        using var answered = await Api.PostJsonAsync(AnswerUrl(item, 1), new AnswerQuestionRequest("Done."));
        answered.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var response = await Api.PostJsonAsync(WithdrawUrl(item, 1));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Conflict)).Code.ShouldBe(ApiErrorCodes.Conflict);
        await using var connection = await OpenDatabaseAsync();
        (await AskRows.QuestionAsync(connection, question)).State.ShouldBe("answered");
    }

    [Test]
    public async Task Answer_WhenNotMember_ShouldBeNotFound()
    {
        var (item, question) = await OpenQuestionAsync();
        var kai = Api.ActingAs(TestUser.Kai);

        using var answer = await kai.PostJsonAsync(AnswerUrl(item, 1), new AnswerQuestionRequest("Hijack."));
        using var withdraw = await kai.PostJsonAsync(WithdrawUrl(item, 1));

        foreach (var response in new[] { answer, withdraw })
        {
            (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
        }

        await using var connection = await OpenDatabaseAsync();
        (await AskRows.QuestionAsync(connection, question)).State.ShouldBe("open");
    }

    private static string AnswerUrl(Guid item, int number) => $"api/work-items/{item}/questions/{number}/answer";

    private static string WithdrawUrl(Guid item, int number) => $"api/work-items/{item}/questions/{number}/withdraw";

    private static async Task<IReadOnlyList<CommentDto>> CommentsAsync(ApiClient client, Guid item)
    {
        using var response = await client.GetAsync($"api/work-items/{item}/comments");
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonSerializer.Deserialize<IReadOnlyList<CommentDto>>(text, WireOptions)!;
    }

    private async Task<AskedQuestion> AskAsync(Guid item, string text)
    {
        Host.ActAs(SeedIds.Users.Joel);
        try
        {
            await using var scope = Host.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IQuestionWrites>().AskAsync(item, text);
        }
        finally
        {
            Host.ActAs(null);
        }
    }

    private async Task<(Guid Item, Guid Question)> OpenQuestionAsync()
    {
        var item = await NewItemAsync();
        await using var connection = await OpenDatabaseAsync();
        var (question, _) = await new QuestionBuilder(item).Number(1).BuildAsync(connection);
        return (item, question);
    }

    private async Task<Guid> NewItemAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        return await new WorkItemBuilder(SeedIds.Projects.Ship).WithAssignee(SeedIds.Users.Joel).InsertAsync(connection);
    }
}
