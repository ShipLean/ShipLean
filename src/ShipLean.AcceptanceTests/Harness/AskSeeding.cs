using Npgsql;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Asks;
using ShipLean.TestSupport.Builders;

namespace ShipLean.AcceptanceTests.Harness;

// Arranges decisions and questions on a work item in the acceptance database, through the TestSupport builders. Pass
// the fixture's connection string (protected on AcceptanceFixture, so a test hands it in). Seed before opening the page.
public static class AskSeeding
{
    // One decision to arrange. State: open | confirmed | changed | withdrawn. A settled decision gets Joel as the answerer.
    // AnswerKind (recommended | alternative | own) and Answer set the standing answer; AlternativeIndex goes with an alternative.
    public sealed record DecisionSeed(
        string Question,
        string Recommended,
        string[]? Alternatives = null,
        string State = "open",
        string? AnswerKind = null,
        string? Answer = null,
        int? AlternativeIndex = null);

    // Adds the decisions in order, each with a raised event, and returns their ids and numbers.
    public static async Task<IReadOnlyList<(Guid Id, int Number)>> SeedDecisionsAsync(
        string connectionString, Guid workItemId, params DecisionSeed[] decisions)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        var created = new List<(Guid Id, int Number)>();
        foreach (var seed in decisions)
        {
            var builder = new DecisionBuilder(workItemId)
                .Question(seed.Question)
                .Recommended(seed.Recommended)
                .Alternatives(seed.Alternatives ?? [])
                .State(seed.State);
            if (seed.AnswerKind is not null) builder.Answer(seed.AnswerKind, seed.Answer ?? seed.Recommended, seed.AlternativeIndex);
            if (seed.State != "open") builder.AnsweredBy(SeedIds.Users.Joel, AskDefaults.Now());
            var decision = await builder.BuildAsync(connection);
            await new AskEventBuilder(workItemId).ForDecision(decision.Id).Kind("raised").BuildAsync(connection);
            created.Add(decision);
        }

        return created;
    }

    // Adds one question with an asked event. An answered question (state "answered") also gets its answer comment, by Joel,
    // with the answer tag, from answerBody. State: open | answered | withdrawn.
    public static async Task<(Guid Id, int Number)> SeedQuestionAsync(
        string connectionString, Guid workItemId, string text = "Arranged question?", string state = "open", string answerBody = "The answer")
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        var builder = new QuestionBuilder(workItemId).Text(text);
        Guid? commentId = null;
        switch (state)
        {
            case "answered":
                commentId = await new CommentBuilder(workItemId).WithId(Guid.NewGuid()).WithAuthor(SeedIds.Users.Joel).WithBody(answerBody).InsertAsync(connection);
                builder.Answered(commentId.Value, SeedIds.Users.Joel, AskDefaults.Now());
                break;
            case "withdrawn":
                builder.Withdrawn(SeedIds.Users.Joel, AskDefaults.Now());
                break;
        }

        var question = await builder.BuildAsync(connection);
        if (commentId is { } id) await new AskTagBuilder().Comment(id).Kind("answer").Question(question.Id).BuildAsync(connection);
        await new AskEventBuilder(workItemId).ForQuestion(question.Id).Kind("asked").BuildAsync(connection);
        return question;
    }
}
