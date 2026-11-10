using Dapper;
using Npgsql;
using ShipLean.Application.Features.Asks.Sql;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Asks;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Asks;

// The asks tables (K1), their numbering lock and writers, the builders and AskRows (K8), and the seeded asks on SHIP-124. The
// database is shared by the class, so every test arranges its own work item.
[TestFixture]
public class AskSqlTests : ApiFixture
{
    [Test]
    public async Task Lock_WhenTwoTransactionsNumberTheSameItem_ShouldGiveDistinctNumbers()
    {
        var item = await NewItemAsync();
        await using var first = await OpenDatabaseAsync();
        await using var second = await OpenDatabaseAsync();
        await using var watcher = await OpenDatabaseAsync();
        await using var a = await first.BeginTransactionAsync();
        await using var b = await second.BeginTransactionAsync();
        var bPid = await second.ExecuteScalarAsync<int>("SELECT pg_backend_pid()", transaction: b);

        await AskNumbers.LockAsync(a, item);
        var numberA = await AskNumbers.NextDecisionAsync(a, item);
        var secondWriter = Task.Run(async () =>
        {
            await AskNumbers.LockAsync(b, item);
            var number = await AskNumbers.NextDecisionAsync(b, item);
            await InsertDecisionAsync(second, b, item, number);
            await b.CommitAsync();
            return number;
        });
        // Poll pg_locks until B is waiting on A's advisory lock; each query paces the loop, and the deadline bounds it.
        var deadline = TimeProvider.System.GetUtcNow().AddSeconds(15);
        var waiting = false;
        while (!waiting && !secondWriter.IsCompleted && TimeProvider.System.GetUtcNow() < deadline)
        {
            waiting = await watcher.ExecuteScalarAsync<bool>(
                "SELECT EXISTS (SELECT 1 FROM pg_locks WHERE pid = @bPid AND locktype = 'advisory' AND NOT granted)", new { bPid });
        }
        waiting.ShouldBeTrue("The second transaction never waited on the first one's lock.");
        await InsertDecisionAsync(first, a, item, numberA);
        await a.CommitAsync();
        var numberB = await secondWriter;

        numberA.ShouldBe(1);
        numberB.ShouldBe(2);
        (await AskRows.DecisionsAsync(first, item)).Select(d => d.Number).ShouldBe([1, 2]);
    }

    [Test]
    public async Task Unique_WhenSameNumberInsertedTwice_ShouldFail()
    {
        var item = await NewItemAsync();
        await using var connection = await OpenDatabaseAsync();
        await new DecisionBuilder(item).Number(1).BuildAsync(connection);
        await new QuestionBuilder(item).Number(1).BuildAsync(connection);

        var decision = await Should.ThrowAsync<PostgresException>(() => new DecisionBuilder(item).Number(1).BuildAsync(connection));
        var question = await Should.ThrowAsync<PostgresException>(() => new QuestionBuilder(item).Number(1).BuildAsync(connection));

        decision.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
        decision.ConstraintName.ShouldBe("ux_decision_number");
        question.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
        question.ConstraintName.ShouldBe("ux_question_number");
    }

    [Test]
    public async Task Check_WhenEventHasBothDecisionAndQuestion_ShouldFail()
    {
        var item = await NewItemAsync();
        await using var connection = await OpenDatabaseAsync();
        var (decision, _) = await new DecisionBuilder(item).BuildAsync(connection);
        var (question, _) = await new QuestionBuilder(item).BuildAsync(connection);

        var error = await Should.ThrowAsync<PostgresException>(
            () => new AskEventBuilder(item).ForDecision(decision).ForQuestion(question).Kind("raised").BuildAsync(connection));

        error.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        error.ConstraintName.ShouldBe("ck_ask_event_target");
    }

    [Test]
    public async Task Check_WhenTagHasNeitherDecisionNorQuestion_ShouldFail()
    {
        var item = await NewItemAsync();
        await using var connection = await OpenDatabaseAsync();
        var comment = await new CommentBuilder(item).WithAuthor(SeedIds.Users.Joel).InsertAsync(connection);

        var error = await Should.ThrowAsync<PostgresException>(() => new AskTagBuilder().Comment(comment).Kind("about").BuildAsync(connection));

        error.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        error.ConstraintName.ShouldBe("ck_comment_ask_tag_target");
    }

    [Test]
    public async Task Numbers_WhenDecisionsAndQuestionsExist_ShouldCountSeparately()
    {
        var item = await NewItemAsync();
        await using var connection = await OpenDatabaseAsync();
        await new DecisionBuilder(item).BuildAsync(connection);
        await new DecisionBuilder(item).BuildAsync(connection);
        await new DecisionBuilder(item).BuildAsync(connection);
        await new QuestionBuilder(item).BuildAsync(connection);
        var other = await NewItemAsync();
        await new QuestionBuilder(other).BuildAsync(connection);
        await new QuestionBuilder(other).BuildAsync(connection);

        await using var transaction = await connection.BeginTransactionAsync();
        await AskNumbers.LockAsync(transaction, item);
        var decision = await AskNumbers.NextDecisionAsync(transaction, item);
        var question = await AskNumbers.NextQuestionAsync(transaction, item);

        decision.ShouldBe(4);
        question.ShouldBe(2);
    }

    [Test]
    public async Task Numbers_WhenHighestIsWithdrawn_ShouldStillNotReuseIt()
    {
        var item = await NewItemAsync();
        await using var connection = await OpenDatabaseAsync();
        await new DecisionBuilder(item).Number(1).BuildAsync(connection);
        await new DecisionBuilder(item).Number(2).State("withdrawn").BuildAsync(connection);
        await new QuestionBuilder(item).Number(1).Withdrawn(SeedIds.Users.Joel, TestDates.Today).BuildAsync(connection);

        await using var transaction = await connection.BeginTransactionAsync();
        await AskNumbers.LockAsync(transaction, item);
        var decision = await AskNumbers.NextDecisionAsync(transaction, item);
        var question = await AskNumbers.NextQuestionAsync(transaction, item);

        decision.ShouldBe(3);
        question.ShouldBe(2);
    }

    [Test]
    public async Task Builders_WhenRoundTripped_ShouldReadBackEveryColumn()
    {
        var item = await NewItemAsync();
        await using var connection = await OpenDatabaseAsync();
        var raised = TestDates.Today.AddDays(-2).AddHours(9);
        var answered = TestDates.Today.AddDays(-1).AddHours(14);
        var comment = await new CommentBuilder(item).WithAuthor(SeedIds.Users.Priya).InsertAsync(connection);

        var (decisionId, decisionNumber) = await new DecisionBuilder(item)
            .Number(5).Question("Which wallet first?").Recommended("Apple Pay").Alternatives("Google Pay", "Both at once")
            .State("changed").Answer("alternative", "Both at once", 1).AnsweredBy(SeedIds.Users.Priya, answered, "Claude Code (review)")
            .RaisedBy(SeedIds.Users.Tom, null, raised).FromDescription()
            .BuildAsync(connection);
        var (answeredId, answeredNumber) = await new QuestionBuilder(item)
            .Number(3).Text("Which merchant ID?").AskedBy(SeedIds.Users.Tom, "Claude Code", raised)
            .Answered(comment, SeedIds.Users.Priya, answered)
            .BuildAsync(connection);
        var (withdrawnId, _) = await new QuestionBuilder(item).Text("Still needed?").Withdrawn(SeedIds.Users.Mei, answered).BuildAsync(connection);
        var eventId = await new AskEventBuilder(item)
            .ForDecision(decisionId).Kind("changed").From("Apple Pay").To("Both at once").ToKind("alternative").Note("Both are ready")
            .By(SeedIds.Users.Priya, "Claude Code (review)").At(answered).FromDescription()
            .BuildAsync(connection);
        var tagged = await new AskTagBuilder().Comment(comment).Kind("answer").Question(answeredId).BuildAsync(connection);

        var decision = await AskRows.DecisionAsync(connection, decisionId);
        var questions = await AskRows.QuestionsAsync(connection, item);
        var events = await AskRows.EventsAsync(connection, item);
        var tag = await AskRows.TagAsync(connection, comment);

        // Records compare arrays by reference, so the alternatives are checked on their own.
        string[] none = [];
        decisionNumber.ShouldBe(5);
        decision.Alternatives.ShouldBe(["Google Pay", "Both at once"]);
        (decision with { Alternatives = none }).ShouldBe(new AskRows.Decision(
            decisionId, item, 5, "Which wallet first?", "Apple Pay", none, "Both at once", "alternative", 1, "changed",
            SeedIds.Users.Tom, null, raised, SeedIds.Users.Priya, "Claude Code (review)", answered, true, answered));
        answeredNumber.ShouldBe(3);
        questions.ShouldBe([
            new AskRows.Question(answeredId, item, 3, "Which merchant ID?", "answered", SeedIds.Users.Tom, "Claude Code", raised,
                comment, SeedIds.Users.Priya, answered, null, null),
            new AskRows.Question(withdrawnId, item, 4, "Still needed?", "withdrawn", SeedIds.Users.Joel, AskDefaults.AgentName,
                questions[1].CreatedAt, null, null, null, SeedIds.Users.Mei, answered),
        ]);
        events.ShouldBe([
            new AskEventRow(eventId, item, decisionId, null, "changed", "Apple Pay", "Both at once", "alternative", "Both are ready",
                SeedIds.Users.Priya, "Claude Code (review)", answered, true),
        ]);
        tagged.ShouldBe(comment);
        tag.ShouldNotBeNull();
        (tag with { CreatedAt = default }).ShouldBe(new AskRows.Tag(comment, "answer", null, answeredId, default));
        (await AskRows.TagsAsync(connection, item)).Select(t => t.CommentId).ShouldBe([comment]);
    }

    [Test]
    public async Task Seed_WhenLoaded_ShouldHaveOpenAsksOnTomsStory()
    {
        await using var connection = await OpenDatabaseAsync();

        var owner = await connection.ExecuteScalarAsync<Guid>(
            "SELECT coalesce(assigned_to, created_by) FROM work_item WHERE id = @id", new { id = SeedIds.Asks.Item });
        var decisions = await AskRows.DecisionsAsync(connection, SeedIds.Asks.Item);
        var questions = await AskRows.QuestionsAsync(connection, SeedIds.Asks.Item);
        var events = await AskRows.EventsAsync(connection, SeedIds.Asks.Item);

        SeedIds.Asks.Item.ShouldBe(SeedIds.WorkItems.Ship124);
        owner.ShouldBe(SeedIds.Users.Tom);
        decisions.Select(d => (d.Id, d.Number, d.State, d.CurrentAnswer == d.Recommended))
            .ShouldBe([(SeedIds.Asks.D1, 1, "open", true), (SeedIds.Asks.D2, 2, "open", true), (SeedIds.Asks.D3, 3, "open", true),
                (SeedIds.Asks.D4, 4, "open", true)]);
        decisions.ShouldAllBe(d => d.Alternatives.Length > 0);
        questions.Select(q => (q.Id, q.Number, q.State)).ShouldBe([(SeedIds.Asks.Q1, 1, "open"), (SeedIds.Asks.Q2, 2, "open")]);
        events.Select(e => (e.DecisionId ?? e.QuestionId, e.Kind)).Order().ShouldBe(
            new[]
            {
                ((Guid?)SeedIds.Asks.D1, "raised"), (SeedIds.Asks.D2, "raised"), (SeedIds.Asks.D3, "raised"), (SeedIds.Asks.D4, "raised"),
                (SeedIds.Asks.Q1, "asked"), (SeedIds.Asks.Q2, "asked"),
            }.Order());
    }

    private async Task<Guid> NewItemAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        return await new WorkItemBuilder(SeedIds.Projects.Ship).InsertAsync(connection);
    }

    private static Task InsertDecisionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid item, int number)
        => connection.ExecuteAsync(
            """
            INSERT INTO decision (id, work_item_id, number, question, recommended, current_answer, created_by_user_id)
            VALUES (@id, @item, @number, 'Locked?', 'Yes', 'Yes', @joel)
            """,
            new { id = Guid.NewGuid(), item, number, joel = SeedIds.Users.Joel }, transaction);
}
