using System.Net;
using System.Text.Json;
using Dapper;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Asks;

// record_decisions, ask, answer_decision and answer_question as Joel, a member. Every test makes its own work item, so numbers
// start at D1 and Q1 and the order does not matter.
[TestFixture]
public class AskToolsTests : McpFixture
{
    private const string Agent = "Test Agent (unverified)";
    private static readonly DateTimeOffset Earlier = new(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);

    protected override bool WithProbeTools => false;

    private async Task<(Guid Id, string Key)> NewItemAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        var id = await new WorkItemBuilder(SeedIds.Projects.Ship).InsertAsync(connection);
        return (id, await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @id", new { id }));
    }

    private async Task NewDecisionAsync(Guid item, int number, string state = "open")
    {
        await using var connection = await OpenDatabaseAsync();
        var builder = new DecisionBuilder(item).Number(number).Question($"Arranged {number}?").Recommended($"Rec {number}").Alternatives("Alt one", "Alt two").State(state);
        if (state == "confirmed")
        {
            builder.AnsweredBy(SeedIds.Users.Joel, Earlier);
        }

        await builder.BuildAsync(connection);
    }

    private async Task NewQuestionAsync(Guid item, int number)
    {
        await using var connection = await OpenDatabaseAsync();
        await new QuestionBuilder(item).Number(number).Text($"Arranged question {number}?").BuildAsync(connection);
    }

    private async Task<(string State, string Answer, int? Index, string? AnsweredAgent, Guid? AnsweredBy, DateOnly? AnsweredOn, string? CreatedAgent)> DecisionAsync(Guid item, int number)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.QuerySingleAsync<(string, string, int?, string?, Guid?, DateOnly?, string?)>(
            "SELECT state, current_answer, alternative_index, answered_agent_name, answered_by_user_id, (answered_at AT TIME ZONE 'UTC')::date, created_agent_name FROM decision WHERE work_item_id = @item AND number = @number",
            new { item, number });
    }

    private async Task<int> DecisionCountAsync(Guid item)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM decision WHERE work_item_id = @item", new { item });
    }

    private async Task<string> QuestionStateAsync(Guid item, int number)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.QuerySingleAsync<string>("SELECT state FROM question WHERE work_item_id = @item AND number = @number", new { item, number });
    }

    private static void ShouldBeValidation(JsonElement error, string field)
    {
        error.GetProperty("code").GetString().ShouldBe(ApiErrorCodes.Validation);
        error.GetProperty("field").GetString().ShouldBe(field);
    }

    [Test]
    public async Task RecordDecisions_WhenNew_ShouldReturnNumbersAndStampAgent()
    {
        var (id, key) = await NewItemAsync();
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "record_decisions",
            $$"""{"item":"{{key}}","decisions":[{"question":"First?","recommended":"Yes","alternatives":["No"]},{"question":"Second?","recommended":"Maybe"}]}""");

        reply.IsToolError().ShouldBeFalse(reply.Body);
        var result = reply.Structured();
        result.GetProperty("created").EnumerateArray().Select(n => n.GetInt32()).ShouldBe([1, 2]);
        result.GetProperty("updated").GetArrayLength().ShouldBe(0);
        result.GetProperty("open_url").GetString()!.ShouldEndWith($"/p/SHIP/items/{key}#D1");
        var texts = reply.Texts();
        texts[0].ShouldBe($"Recorded D1, D2 on {key} (2 new)");
        texts.ShouldContain(t => t.Contains("- D1 (new): First? → Yes · open") && t.Contains("- D2 (new): Second? → Maybe · open"));
        (await DecisionAsync(id, 1)).CreatedAgent.ShouldBe(Agent);
    }

    [Test]
    public async Task RecordDecisions_WhenOpenResentUnchangedAndOneEdited_ShouldReportUpdatedAndUnchanged()
    {
        var (id, key) = await NewItemAsync();
        await NewDecisionAsync(id, 1);
        await NewDecisionAsync(id, 2);
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "record_decisions",
            $$"""{"item":"{{key}}","decisions":[{"number":1,"question":"Arranged 1?","recommended":"Rec 1","alternatives":["Alt one","Alt two"]},{"number":2,"question":"Arranged 2 edited?","recommended":"Rec 2","alternatives":["Alt one","Alt two"]}]}""");

        reply.IsToolError().ShouldBeFalse(reply.Body);
        reply.Structured().GetProperty("created").GetArrayLength().ShouldBe(0);
        reply.Structured().GetProperty("updated").EnumerateArray().Select(n => n.GetInt32()).ShouldBe([2]);
        reply.Texts()[0].ShouldBe($"Recorded D1, D2 on {key} (1 updated, 1 unchanged)");
        reply.Texts().ShouldContain(t => t.Contains("- D1 (unchanged):") && t.Contains("- D2 (updated):"));
    }

    [Test]
    public async Task RecordDecisions_WhenSettledDecisionEdited_ShouldReturnValidationError()
    {
        var (id, key) = await NewItemAsync();
        await NewDecisionAsync(id, 1, "confirmed");
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "record_decisions",
            $$"""{"item":"{{key}}","decisions":[{"question":"New one?","recommended":"Yes"},{"number":1,"question":"Edited?","recommended":"Other"}]}""");

        reply.IsToolError().ShouldBeTrue();
        var error = reply.Structured().GetProperty("error");
        ShouldBeValidation(error, "decisions");
        error.GetProperty("message").GetString()!.ShouldStartWith("D1: ");
        (await DecisionCountAsync(id)).ShouldBe(1);
    }

    [Test]
    public async Task RecordDecisions_WhenSettledGiven_ShouldStoreConfirmed()
    {
        var (id, key) = await NewItemAsync();
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "record_decisions",
            $$$"""{"item":"{{{key}}}","decisions":[{"number":1,"question":"Backfilled?","recommended":"Rec","alternatives":["First"],"settled":{"by":"me","on":"2026-10-01","answer":"recommended"}}]}""");

        reply.IsToolError().ShouldBeFalse(reply.Body);
        var row = await DecisionAsync(id, 1);
        row.State.ShouldBe("confirmed");
        row.Answer.ShouldBe("Rec");
        row.Index.ShouldBeNull();
        row.AnsweredBy.ShouldBe(SeedIds.Users.Joel);
        row.AnsweredOn.ShouldBe(new DateOnly(2026, 10, 1));
    }

    [Test]
    public async Task RecordDecisions_WhenSettled_ShouldCreateChangedWithAlternativeCountedFromOne()
    {
        var (id, key) = await NewItemAsync();
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "record_decisions",
            $$$"""{"item":"{{{key}}}","decisions":[{"number":1,"question":"Backfilled?","recommended":"Rec","alternatives":["First","Second"],"settled":{"by":"me","on":"2026-10-01","answer":"alternative:2"}}]}""");

        reply.IsToolError().ShouldBeFalse(reply.Body);
        var row = await DecisionAsync(id, 1);
        row.State.ShouldBe("changed");
        row.Answer.ShouldBe("Second");
        row.Index.ShouldBe(1);
        row.AnsweredBy.ShouldBe(SeedIds.Users.Joel);
    }

    [Test]
    public async Task RecordDecisions_WhenSettledDayMalformed_ShouldBeValidationOnDecisionsNamingTheDecision()
    {
        var (id, key) = await NewItemAsync();
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "record_decisions",
            $$$"""{"item":"{{{key}}}","decisions":[{"number":2,"question":"Bad day?","recommended":"Rec","settled":{"by":"me","on":"1 Oct","answer":"recommended"}}]}""");

        reply.IsToolError().ShouldBeTrue();
        var error = reply.Structured().GetProperty("error");
        ShouldBeValidation(error, "decisions");
        error.GetProperty("message").GetString()!.ShouldStartWith("D2: ");
        (await DecisionCountAsync(id)).ShouldBe(0);
    }

    [Test]
    public async Task RecordDecisions_WhenItemOutsideProjectAndBadSettledDay_ShouldBeNotFoundBeforeValidation()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "record_decisions",
            """{"item":"PART-1","decisions":[{"question":"Q?","recommended":"A","settled":{"by":"me","on":"1 Oct","answer":"recommended"}}]}""");

        reply.IsToolError().ShouldBeTrue();
        reply.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Ask_WhenCalled_ShouldReturnQ1AndOpenUrl()
    {
        var (id, key) = await NewItemAsync();
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "ask", $$"""{"item":"{{key}}","text":"Which colour?"}""");

        reply.IsToolError().ShouldBeFalse(reply.Body);
        var result = reply.Structured();
        result.GetProperty("number").GetInt32().ShouldBe(1);
        result.GetProperty("label").GetString().ShouldBe("Q1");
        result.GetProperty("open_url").GetString()!.ShouldEndWith($"/p/SHIP/items/{key}#Q1");
        reply.Texts()[0].ShouldBe($"Asked Q1 on {key}; the owner answers on the item and the next agent to pick it up reads it.");
        await using var connection = await OpenDatabaseAsync();
        (await connection.QuerySingleAsync<string>(
            "SELECT created_agent_name FROM question WHERE work_item_id = @id AND number = 1", new { id })).ShouldBe(Agent);
    }

    [Test]
    public async Task Ask_WhenOptionsPassed_ShouldRecordThem()
    {
        var (id, key) = await NewItemAsync();
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "ask", $$"""{"item":"{{key}}","text":"Which colour?","options":[" Red ","Blue"]}""");

        reply.IsToolError().ShouldBeFalse(reply.Body);
        await using var connection = await OpenDatabaseAsync();
        (await connection.QuerySingleAsync<string>(
            "SELECT options::text FROM question WHERE work_item_id = @id AND number = 1", new { id })).ShouldBe("""["Red", "Blue"]""");
    }

    [Test]
    public async Task Ask_WhenSevenOptionsPassed_ShouldReturnValidationError()
    {
        var (id, key) = await NewItemAsync();
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "ask", $$"""{"item":"{{key}}","text":"Which?","options":["1","2","3","4","5","6","7"]}""");

        reply.IsToolError().ShouldBeTrue();
        ShouldBeValidation(reply.Structured().GetProperty("error"), "options");
        await using var connection = await OpenDatabaseAsync();
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM question WHERE work_item_id = @id", new { id })).ShouldBe(0);
    }

    [Test]
    public async Task AnswerDecision_WhenConfirm_ShouldConfirm()
    {
        var (id, key) = await NewItemAsync();
        await NewDecisionAsync(id, 1);
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "answer_decision", $$"""{"item":"{{key}}","number":1,"action":"confirm"}""");

        reply.IsToolError().ShouldBeFalse(reply.Body);
        reply.Structured().GetProperty("state").GetString().ShouldBe("confirmed");
        var row = await DecisionAsync(id, 1);
        row.State.ShouldBe("confirmed");
        row.AnsweredAgent.ShouldBe(Agent);
    }

    [Test]
    public async Task AnswerDecision_WhenConfirmWithAnswer_ShouldBeValidationOnAnswerAndStayOpen()
    {
        var (id, key) = await NewItemAsync();
        await NewDecisionAsync(id, 1);
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "answer_decision", $$"""{"item":"{{key}}","number":1,"action":"confirm","answer":"recommended"}""");

        reply.IsToolError().ShouldBeTrue();
        ShouldBeValidation(reply.Structured().GetProperty("error"), "answer");
        (await DecisionAsync(id, 1)).State.ShouldBe("open");
    }

    [Test]
    public async Task AnswerDecision_WhenChangeToAlternativeOne_ShouldStoreIndexZero()
    {
        var (id, key) = await NewItemAsync();
        await NewDecisionAsync(id, 1);
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "answer_decision", $$"""{"item":"{{key}}","number":1,"action":"change","answer":"alternative:1","note":"Cheaper"}""");

        reply.IsToolError().ShouldBeFalse(reply.Body);
        var row = await DecisionAsync(id, 1);
        row.State.ShouldBe("changed");
        row.Answer.ShouldBe("Alt one");
        row.Index.ShouldBe(0);
    }

    [Test]
    public async Task AnswerDecision_WhenChangeToOwnWords_ShouldStoreTheText()
    {
        var (id, key) = await NewItemAsync();
        await NewDecisionAsync(id, 1);
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "answer_decision", $$"""{"item":"{{key}}","number":1,"action":"change","answer":"Use the vendor default"}""");

        reply.IsToolError().ShouldBeFalse(reply.Body);
        reply.Structured().GetProperty("current_answer").GetString().ShouldBe("Use the vendor default");
        (await DecisionAsync(id, 1)).State.ShouldBe("changed");
    }

    [Test]
    public async Task AnswerDecision_WhenWithdraw_ShouldWithdraw()
    {
        var (id, key) = await NewItemAsync();
        await NewDecisionAsync(id, 1);
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "answer_decision", $$"""{"item":"{{key}}","number":1,"action":"withdraw","note":"No longer needed"}""");

        reply.IsToolError().ShouldBeFalse(reply.Body);
        (await DecisionAsync(id, 1)).State.ShouldBe("withdrawn");
    }

    [Test]
    public async Task AnswerDecision_WhenChangeWithoutAnswer_ShouldReturnValidationError()
    {
        var (id, key) = await NewItemAsync();
        await NewDecisionAsync(id, 1);
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "answer_decision", $$"""{"item":"{{key}}","number":1,"action":"change"}""");

        reply.IsToolError().ShouldBeTrue();
        ShouldBeValidation(reply.Structured().GetProperty("error"), "answer");
        (await DecisionAsync(id, 1)).State.ShouldBe("open");
    }

    [TestCase("alternative:0")]
    [TestCase("alternative:x")]
    [TestCase("  ")]
    public async Task AnswerDecision_WhenAnswerMalformed_ShouldBeValidationOnAnswer(string answer)
    {
        var (id, key) = await NewItemAsync();
        await NewDecisionAsync(id, 1);
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "answer_decision", $$"""{"item":"{{key}}","number":1,"action":"change","answer":"{{answer}}"}""");

        reply.IsToolError().ShouldBeTrue();
        ShouldBeValidation(reply.Structured().GetProperty("error"), "answer");
        (await DecisionAsync(id, 1)).State.ShouldBe("open");
    }

    [Test]
    public async Task AnswerDecision_WhenAlreadyConfirmed_ShouldBeConflictFromTheService()
    {
        var (id, key) = await NewItemAsync();
        await NewDecisionAsync(id, 1, "confirmed");
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var again = await CallToolAsync(token, "answer_decision", $$"""{"item":"{{key}}","number":1,"action":"confirm"}""");

        again.IsToolError().ShouldBeTrue();
        again.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe(ApiErrorCodes.Conflict);
    }

    [Test]
    public async Task AnswerDecision_WhenItemOutsideProjectAndBadAction_ShouldBeNotFoundBeforeValidation()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "answer_decision", """{"item":"PART-1","number":1,"action":"bogus"}""");

        reply.IsToolError().ShouldBeTrue();
        reply.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task AnswerQuestion_WhenOpen_ShouldPostAnswerComment()
    {
        var (id, key) = await NewItemAsync();
        await NewQuestionAsync(id, 1);
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "answer_question", $$"""{"item":"{{key}}","number":1,"answer":"Use **merchant.prod**"}""");

        reply.IsToolError().ShouldBeFalse(reply.Body);
        var commentId = reply.Structured().GetProperty("comment_id").GetGuid();
        await using var connection = await OpenDatabaseAsync();
        var row = await connection.QuerySingleAsync<(string State, Guid? Comment)>(
            "SELECT state, answer_comment_id FROM question WHERE work_item_id = @id AND number = 1", new { id });
        row.ShouldBe(("answered", commentId));
    }

    [TestCase("")]
    [TestCase("   ")]
    public async Task AnswerQuestion_WhenAnswerBlank_ShouldBeValidationOnAnswer(string answer)
    {
        var (id, key) = await NewItemAsync();
        await NewQuestionAsync(id, 1);
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "answer_question", $$"""{"item":"{{key}}","number":1,"answer":"{{answer}}"}""");

        reply.IsToolError().ShouldBeTrue();
        ShouldBeValidation(reply.Structured().GetProperty("error"), "answer");
        (await QuestionStateAsync(id, 1)).ShouldBe("open");
    }

    [Test]
    public async Task Tools_WhenReadOnlyToken_ShouldRefuseWrites()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);
        var calls = new[]
        {
            ("record_decisions", """{"item":"SHIP-1","decisions":[{"question":"Q?","recommended":"A"}]}"""),
            ("ask", """{"item":"SHIP-1","text":"Why?"}"""),
            ("answer_decision", """{"item":"SHIP-1","number":1,"action":"confirm"}"""),
            ("answer_question", """{"item":"SHIP-1","number":1,"answer":"Because"}"""),
        };

        foreach (var (tool, arguments) in calls)
        {
            var reply = await CallToolAsync(token, tool, arguments);

            reply.Status.ShouldBe(HttpStatusCode.Forbidden, tool);
            reply.WwwAuthenticate.ShouldContain("insufficient_scope");
        }
    }
}
