using Dapper;
using Npgsql;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Asks;
using ShipLean.TestSupport.Builders;

namespace ShipLean.AcceptanceTests.Harness;

// Arranges decisions and questions on a work item in the acceptance database, through the TestSupport builders. Pass
// the fixture's connection string (protected on AcceptanceFixture, so a test hands it in). Seed before opening the page.
public static class AskSeeding
{
    // A fresh work item in project SHIP, so a test owns its asks. Pass parentId for a child (a story under a Feature), and
    // assignee to give it an owner (an ask is for the assignee, or the creator; builder items have no creator).
    public static async Task<(Guid Id, string Key)> SeedItemAsync(
        string connectionString, string type = "story", string title = "Arranged asks item", Guid? parentId = null,
        Guid? assignee = null)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        var builder = new WorkItemBuilder(SeedIds.Projects.Ship).WithType(type).WithTitle(title);
        if (parentId is { } parent) builder.WithParent(parent);
        if (assignee is { } userId) builder.WithAssignee(userId);
        var id = await builder.InsertAsync(connection);
        var key = await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @id", new { id });
        return (id, key);
    }

    // One decision to arrange. State: open | confirmed | changed | withdrawn. A settled decision gets Joel as the answerer.
    // AnswerKind (recommended | alternative | own) sets the standing answer: recommended -> Recommended; alternative -> the
    // alternative at AlternativeIndex (Answer overrides); own -> Answer, required (throws without it). An open decision must not
    // set AnswerKind (the table holds the recommended answer while open): that is a caller error and throws.
    // A settled decision also gets its settle event (confirmed | changed | withdrawn), so History matches the state; Note goes on it.
    public sealed record DecisionSeed(
        string Question,
        string Recommended,
        string[]? Alternatives = null,
        string State = "open",
        string? AnswerKind = null,
        string? Answer = null,
        int? AlternativeIndex = null,
        string? Note = null);

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
            var alternatives = seed.Alternatives ?? [];
            var answer = seed.Recommended;
            if (seed.AnswerKind is not null)
            {
                if (seed.State == "open") throw new ArgumentException("An open decision holds the recommended answer: leave AnswerKind unset.", nameof(decisions));
                answer = seed.AnswerKind switch
                {
                    "recommended" => seed.Recommended,
                    "alternative" => seed.Answer ?? alternatives[seed.AlternativeIndex ?? throw new ArgumentException("An alternative answer needs AlternativeIndex.", nameof(decisions))],
                    "own" => seed.Answer ?? throw new ArgumentException("An own answer needs Answer.", nameof(decisions)),
                    _ => throw new ArgumentException($"Unknown AnswerKind {seed.AnswerKind}.", nameof(decisions)),
                };
                builder.Answer(seed.AnswerKind, answer, seed.AnswerKind == "alternative" ? seed.AlternativeIndex : null);
            }

            var settledAt = AskDefaults.Now();
            if (seed.State != "open") builder.AnsweredBy(SeedIds.Users.Joel, settledAt);
            var decision = await builder.BuildAsync(connection);
            await new AskEventBuilder(workItemId).ForDecision(decision.Id).Kind("raised").BuildAsync(connection);
            var kind = seed.AnswerKind ?? "recommended";
            switch (seed.State)
            {
                case "confirmed":
                    await new AskEventBuilder(workItemId).ForDecision(decision.Id).Kind("confirmed").To(answer).ToKind(kind).Note(seed.Note).At(settledAt).BuildAsync(connection);
                    break;
                case "changed":
                    await new AskEventBuilder(workItemId).ForDecision(decision.Id).Kind("changed").From(seed.Recommended).To(answer).ToKind(kind).Note(seed.Note).At(settledAt).BuildAsync(connection);
                    break;
                case "withdrawn":
                    await new AskEventBuilder(workItemId).ForDecision(decision.Id).Kind("withdrawn").From(answer).Note(seed.Note).At(settledAt).BuildAsync(connection);
                    break;
            }

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
                commentId = await new CommentBuilder(workItemId).WithId(Guid.NewGuid()).WithAuthor(SeedIds.Users.Joel).WithBody(answerBody).WithCreatedAt(AskDefaults.Now()).InsertAsync(connection);
                builder.Answered(commentId.Value, SeedIds.Users.Joel, AskDefaults.Now());
                break;
            case "withdrawn":
                builder.Withdrawn(SeedIds.Users.Joel, AskDefaults.Now());
                break;
        }

        var question = await builder.BuildAsync(connection);
        if (commentId is { } id) await new AskTagBuilder().Comment(id).Kind("answer").Question(question.Id).BuildAsync(connection);
        await new AskEventBuilder(workItemId).ForQuestion(question.Id).Kind("asked").BuildAsync(connection);
        if (state == "answered") await new AskEventBuilder(workItemId).ForQuestion(question.Id).Kind("answered").BuildAsync(connection);
        if (state == "withdrawn") await new AskEventBuilder(workItemId).ForQuestion(question.Id).Kind("withdrawn").BuildAsync(connection);
        return question;
    }
}
