using System.Net;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Asks;

// record_decisions, ask, answer_decision and answer_question against the seeded SHIP-124 (Tom's Story: D1-D4 and Q1-Q2 open), as Joel,
// a member. Each test touches its own numbers, so the order does not matter.
[TestFixture]
public class AskToolsTests : McpFixture
{
    private const string Item = "SHIP-124";
    private const string Agent = "Test Agent (unverified)";

    protected override bool WithProbeTools => false;

    private async Task<(string State, string Answer, int? Index, string? AnsweredAgent, Guid? AnsweredBy, string? CreatedAgent)> DecisionAsync(int number)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.QuerySingleAsync<(string, string, int?, string?, Guid?, string?)>(
            "SELECT state, current_answer, alternative_index, answered_agent_name, answered_by_user_id, created_agent_name FROM decision WHERE work_item_id = @item AND number = @number",
            new { item = SeedIds.Asks.Item, number });
    }

    private async Task<string> RecordAsync(int number, string alternatives = """["Alt one","Alt two"]""")
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);
        var reply = await CallToolAsync(token, "record_decisions", $$"""{"item":"{{Item}}","decisions":[{"number":{{number}},"question":"Arranged {{number}}?","recommended":"Rec {{number}}","alternatives":{{alternatives}}}]}""");
        reply.IsToolError().ShouldBeFalse(reply.Body);
        return "ok";
    }

    [Test]
    public async Task RecordDecisions_WhenNewDecisions_ShouldCreateInOrderStampAgentAndLinkFirst()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "record_decisions",
            $$"""{"item":"{{Item}}","decisions":[{"number":60,"question":"Sixty?","recommended":"Yes","alternatives":["No"]},{"question":"Next?","recommended":"Maybe"}]}""");

        reply.IsToolError().ShouldBeFalse(reply.Body);
        var result = reply.Structured();
        var created = result.GetProperty("created").EnumerateArray().Select(n => n.GetInt32()).ToList();
        created.Count.ShouldBe(2);
        created[0].ShouldBe(60);
        result.GetProperty("updated").GetArrayLength().ShouldBe(0);
        result.GetProperty("open_url").GetString()!.ShouldEndWith("/p/SHIP/items/SHIP-124#D60");
        var texts = reply.Texts();
        texts[0].ShouldBe($"Recorded D60, D{created[1]} on SHIP-124 (2 new)");
        texts.ShouldContain(t => t.Contains("- D60 (new): Sixty? → Yes · open") && t.Contains($"- D{created[1]} (new): Next? → Maybe · open"));
        (await DecisionAsync(60)).CreatedAgent.ShouldBe(Agent);
    }

    [Test]
    public async Task RecordDecisions_WhenOpenResentUnchangedAndOneEdited_ShouldReportUpdatedAndUnchanged()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "record_decisions",
            $$"""{"item":"{{Item}}","decisions":[{"number":2,"question":"Which browsers show the button?","recommended":"Only browsers where Apple Pay is set up","alternatives":["Every Safari browser","Every browser, disabled where unavailable"]},{"number":4,"question":"Do we keep the card form when Apple Pay is offered?","recommended":"Yes, always visible","alternatives":["No, hide it"]}]}""");

        reply.IsToolError().ShouldBeFalse(reply.Body);
        reply.Structured().GetProperty("created").GetArrayLength().ShouldBe(0);
        reply.Structured().GetProperty("updated").EnumerateArray().Select(n => n.GetInt32()).ShouldBe([4]);
        reply.Texts()[0].ShouldBe("Recorded D2, D4 on SHIP-124 (1 updated, 1 unchanged)");
        reply.Texts().ShouldContain(t => t.Contains("- D2 (unchanged):") && t.Contains("- D4 (updated):"));
    }

    [Test]
    public async Task RecordDecisions_WhenSettled_ShouldCreateChangedWithAlternativeCountedFromOne()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "record_decisions",
            $$"""{"item":"{{Item}}","decisions":[{"number":61,"question":"Backfilled?","recommended":"Rec","alternatives":["First","Second"],"settled":{"by":"me","on":"2026-10-01","answer":"alternative:2"}}]}""");

        reply.IsToolError().ShouldBeFalse(reply.Body);
        var row = await DecisionAsync(61);
        row.State.ShouldBe("changed");
        row.Answer.ShouldBe("Second");
        row.Index.ShouldBe(1);
        row.AnsweredBy.ShouldBe(SeedIds.Users.Joel);
    }

    [Test]
    public async Task RecordDecisions_WhenSettledDayMalformed_ShouldBeValidationOnDecisionsNamingTheDecision()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "record_decisions",
            $$"""{"item":"{{Item}}","decisions":[{"number":62,"question":"Bad day?","recommended":"Rec","settled":{"by":"me","on":"1 Oct","answer":"recommended"}}]}""");

        reply.IsToolError().ShouldBeTrue();
        var error = reply.Structured().GetProperty("error");
        error.GetProperty("code").GetString().ShouldBe(ApiErrorCodes.Validation);
        error.GetProperty("field").GetString().ShouldBe("decisions");
        error.GetProperty("message").GetString()!.ShouldStartWith("D62: ");
    }

    [Test]
    public async Task Ask_WhenAsked_ShouldNumberItStampAgentAndLinkIt()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "ask", $$"""{"item":"{{Item}}","text":"Which colour?"}""");

        reply.IsToolError().ShouldBeFalse(reply.Body);
        var result = reply.Structured();
        var label = result.GetProperty("label").GetString()!;
        label.ShouldBe($"Q{result.GetProperty("number").GetInt32()}");
        result.GetProperty("open_url").GetString()!.ShouldEndWith($"/p/SHIP/items/SHIP-124#{label}");
        await using var connection = await OpenDatabaseAsync();
        (await connection.QuerySingleAsync<string>(
            "SELECT created_agent_name FROM question WHERE work_item_id = @item AND text = 'Which colour?'", new { item = SeedIds.Asks.Item })).ShouldBe(Agent);
    }

    [Test]
    public async Task AnswerDecision_WhenConfirm_ShouldConfirmAndStampAgent()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "answer_decision", $$"""{"item":"{{Item}}","number":1,"action":"confirm"}""");

        reply.IsToolError().ShouldBeFalse(reply.Body);
        reply.Structured().GetProperty("state").GetString().ShouldBe("confirmed");
        var row = await DecisionAsync(1);
        row.State.ShouldBe("confirmed");
        row.AnsweredAgent.ShouldBe(Agent);
    }

    [Test]
    public async Task AnswerDecision_WhenChangeToAlternativeOne_ShouldStoreIndexZero()
    {
        await RecordAsync(70);
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "answer_decision", $$"""{"item":"{{Item}}","number":70,"action":"change","answer":"alternative:1","note":"Cheaper"}""");

        reply.IsToolError().ShouldBeFalse(reply.Body);
        var row = await DecisionAsync(70);
        row.State.ShouldBe("changed");
        row.Answer.ShouldBe("Alt one");
        row.Index.ShouldBe(0);
    }

    [Test]
    public async Task AnswerDecision_WhenChangeToOwnWords_ShouldStoreTheText()
    {
        await RecordAsync(71);
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "answer_decision", $$"""{"item":"{{Item}}","number":71,"action":"change","answer":"Use the vendor default"}""");

        reply.IsToolError().ShouldBeFalse(reply.Body);
        reply.Structured().GetProperty("current_answer").GetString().ShouldBe("Use the vendor default");
        (await DecisionAsync(71)).State.ShouldBe("changed");
    }

    [Test]
    public async Task AnswerDecision_WhenWithdraw_ShouldWithdraw()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "answer_decision", $$"""{"item":"{{Item}}","number":3,"action":"withdraw","note":"No longer needed"}""");

        reply.IsToolError().ShouldBeFalse(reply.Body);
        (await DecisionAsync(3)).State.ShouldBe("withdrawn");
    }

    [TestCase("""{"number":72,"action":"change","answer":"alternative:0"}""")]
    [TestCase("""{"number":72,"action":"change","answer":"alternative:x"}""")]
    [TestCase("""{"number":72,"action":"change","answer":"  "}""")]
    public async Task AnswerDecision_WhenAnswerMalformed_ShouldBeValidationOnAnswer(string arguments)
    {
        await RecordAsync(72);
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "answer_decision", $$"""{"item":"{{Item}}",{{arguments[1..]}}""");

        reply.IsToolError().ShouldBeTrue();
        var error = reply.Structured().GetProperty("error");
        error.GetProperty("code").GetString().ShouldBe(ApiErrorCodes.Validation);
        error.GetProperty("field").GetString().ShouldBe("answer");
        (await DecisionAsync(72)).State.ShouldBe("open");
    }

    [Test]
    public async Task AnswerDecision_WhenAlreadyConfirmed_ShouldBeConflictFromTheService()
    {
        await RecordAsync(73);
        var token = await TokenAsync(TestUser.Joel, ReadWrite);
        (await CallToolAsync(token, "answer_decision", $$"""{"item":"{{Item}}","number":73,"action":"confirm"}""")).IsToolError().ShouldBeFalse();

        var again = await CallToolAsync(token, "answer_decision", $$"""{"item":"{{Item}}","number":73,"action":"confirm"}""");

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
    public async Task AnswerQuestion_WhenAnswered_ShouldPostCommentAndMarkAnswered()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "answer_question", $$"""{"item":"{{Item}}","number":1,"answer":"Use **merchant.prod**"}""");

        reply.IsToolError().ShouldBeFalse(reply.Body);
        var commentId = reply.Structured().GetProperty("comment_id").GetGuid();
        await using var connection = await OpenDatabaseAsync();
        var row = await connection.QuerySingleAsync<(string State, Guid? Comment)>(
            "SELECT state, answer_comment_id FROM question WHERE work_item_id = @item AND number = 1", new { item = SeedIds.Asks.Item });
        row.ShouldBe(("answered", commentId));
    }

    [Test]
    public async Task AskTools_WhenReadOnlyToken_ShouldRefuseWrites()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);
        var calls = new[]
        {
            ("record_decisions", $$"""{"item":"{{Item}}","decisions":[{"question":"Q?","recommended":"A"}]}"""),
            ("ask", $$"""{"item":"{{Item}}","text":"Why?"}"""),
            ("answer_decision", $$"""{"item":"{{Item}}","number":4,"action":"confirm"}"""),
            ("answer_question", $$"""{"item":"{{Item}}","number":2,"answer":"Because"}"""),
        };

        foreach (var (tool, arguments) in calls)
        {
            var reply = await CallToolAsync(token, tool, arguments);

            reply.Status.ShouldBe(HttpStatusCode.Forbidden, tool);
            reply.WwwAuthenticate.ShouldContain("insufficient_scope");
        }
    }
}
