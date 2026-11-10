using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Application.Features.Asks.Sql;
using ShipLean.Contracts;
using ShipLean.Contracts.Asks;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Asks;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Contracts.Mcp;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.Sprints.Api;

namespace ShipLean.WebApi.Tests.Asks;

// IDecisionRecorder (SV-D1) on the real host and database: each test resolves the recorder from a scope, acts as Joel with a
// settable agent, and asserts what was stored through AskRows. Every test arranges its own work item.
[TestFixture]
public class DecisionRecorderTests : ApiFixture
{
    private readonly SettableCurrentAgent _agent = new();

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.RemoveAll<ICurrentAgent>();
        services.AddSingleton<ICurrentAgent>(_agent);
    };

    [SetUp]
    public void NoAgentByDefault() => _agent.Clear();

    [Test]
    public async Task Record_WhenNoNumbers_ShouldNumberFromOne()
    {
        var item = await NewItemAsync();

        var recorded = await RecordAsync(item, Open(null, "Which card network?", "Visa", "Mastercard"), Open(null, "Which wallet?", "Apple Pay"));

        recorded.Created.ShouldBe([1, 2]);
        recorded.Updated.ShouldBeEmpty();
        await using var connection = await OpenDatabaseAsync();
        var decisions = await AskRows.DecisionsAsync(connection, item);
        decisions.Select(d => (d.Number, d.Question, d.Recommended, d.CurrentAnswer, d.AnswerKind, d.State, d.FromDescription))
            .ShouldBe([(1, "Which card network?", "Visa", "Visa", "recommended", "open", false), (2, "Which wallet?", "Apple Pay", "Apple Pay", "recommended", "open", false)]);
        decisions[0].Alternatives.ShouldBe(["Mastercard"]);
        decisions[0].CreatedByUserId.ShouldBe(SeedIds.Users.Joel);
        var events = await AskRows.EventsAsync(connection, item);
        events.Select(e => (e.DecisionId, e.Kind, e.ToAnswer, e.ToKind, e.ByUserId, e.FromDescription))
            .ShouldBe(
                [(decisions[0].Id, "raised", "Visa", "recommended", SeedIds.Users.Joel, false), (decisions[1].Id, "raised", "Apple Pay", "recommended", SeedIds.Users.Joel, false)],
                ignoreOrder: true);
    }

    [Test]
    public async Task Record_WhenNumberGivenAndFree_ShouldUseIt()
    {
        var item = await NewItemAsync();
        await using var connection = await OpenDatabaseAsync();
        await new DecisionBuilder(item).Number(1).BuildAsync(connection);

        var recorded = await RecordAsync(item, Open(4, "Ship behind a flag?", "Yes"), Open(null, "Who reviews?", "Priya"));

        recorded.Created.ShouldBe([4, 5]);
        (await AskRows.DecisionsAsync(connection, item)).Select(d => (d.Number, d.Question)).ShouldBe(
            [(1, "Arranged decision?"), (4, "Ship behind a flag?"), (5, "Who reviews?")]);
    }

    [Test]
    public async Task Record_WhenOpenNumberGiven_ShouldEditAndAddEditedEvent()
    {
        var item = await NewItemAsync();
        await using var connection = await OpenDatabaseAsync();
        var (id, _) = await new DecisionBuilder(item).Number(1).Question("Old question?").Recommended("Old answer").Alternatives("Other").BuildAsync(connection);

        var recorded = await RecordAsync(item, Open(1, "New question?", "New answer", "Other", "Third"));

        recorded.Created.ShouldBeEmpty();
        recorded.Updated.ShouldBe([1]);
        var decision = await AskRows.DecisionAsync(connection, id);
        (decision.Question, decision.Recommended, decision.CurrentAnswer, decision.AnswerKind, decision.State)
            .ShouldBe(("New question?", "New answer", "New answer", "recommended", "open"));
        decision.Alternatives.ShouldBe(["Other", "Third"]);
        var edited = (await AskRows.EventsAsync(connection, item)).ShouldHaveSingleItem();
        (edited.DecisionId, edited.Kind, edited.FromAnswer, edited.ToAnswer, edited.ByUserId).ShouldBe((id, "edited", "Old question?", "New question?", SeedIds.Users.Joel));
    }

    [Test]
    public async Task Record_WhenOneIsSettled_ShouldRefuseAndWriteNothing()
    {
        var item = await NewItemAsync();
        await using var connection = await OpenDatabaseAsync();
        await new DecisionBuilder(item).Number(1).BuildAsync(connection);
        await new DecisionBuilder(item).Number(2).State("confirmed").AnsweredBy(SeedIds.Users.Joel, TestDates.Today).BuildAsync(connection);
        var before = await AskRows.DecisionsAsync(connection, item);

        // D3 is new and D1 an open edit, both before the refused D2: neither may survive the refusal.
        var refused = await Should.ThrowAsync<ApiException>(
            () => RecordAsync(item, Open(3, "New one?", "Yes"), Open(1, "Edited?", "Yes"), Open(2, "Settled one?", "Yes")));

        refused.Code.ShouldBe(ApiErrorCodes.Validation);
        refused.Field.ShouldBe("decisions");
        var after = await AskRows.DecisionsAsync(connection, item);
        after.Select(d => (d.Number, d.Question, d.Recommended, d.State)).ShouldBe(before.Select(d => (d.Number, d.Question, d.Recommended, d.State)));
        (await AskRows.EventsAsync(connection, item)).ShouldBeEmpty();
    }

    [Test]
    public async Task Record_WhenTwoCallsRaceOnOneItem_ShouldNotCollide()
    {
        var item = await NewItemAsync();
        await using var holder = await OpenDatabaseAsync();
        await using var watcher = await OpenDatabaseAsync();
        await using var held = await holder.BeginTransactionAsync();
        await AskNumbers.LockAsync(held, item);

        Host.ActAs(SeedIds.Users.Joel);
        try
        {
            var first = Task.Run(() => RecordAsActingUserAsync(item, Open(null, "First?", "Yes")));
            var second = Task.Run(() => RecordAsActingUserAsync(item, Open(null, "Second?", "Yes")));
            // Poll pg_locks until both calls wait on the held lock; each query paces the loop, and the deadline bounds it.
            var deadline = TimeProvider.System.GetUtcNow().AddSeconds(15);
            var waiting = 0;
            while (waiting < 2 && !first.IsCompleted && !second.IsCompleted && TimeProvider.System.GetUtcNow() < deadline)
            {
                waiting = await watcher.ExecuteScalarAsync<int>("""
                    SELECT count(*)::int FROM pg_locks
                    WHERE locktype = 'advisory' AND NOT granted AND database = (SELECT oid FROM pg_database WHERE datname = current_database())
                    """);
            }

            waiting.ShouldBe(2, "Both recorders should wait on the item's numbering lock.");
            await held.CommitAsync();
            var numbers = (await Task.WhenAll(first, second)).SelectMany(r => r.Created).Order();

            numbers.ShouldBe([1, 2]);
        }
        finally
        {
            Host.ActAs(null);
        }

        (await AskRows.DecisionsAsync(holder, item)).Select(d => d.Number).ShouldBe([1, 2]);
    }

    [Test]
    public async Task Record_WhenSettledGiven_ShouldStoreConfirmedFromDescription()
    {
        var item = await NewItemAsync();
        var on = DateOnly.FromDateTime(TestDates.Today.AddDays(-3).UtcDateTime);
        _agent.ActAs("Claude Code", "claude.ai", true);

        var recorded = await RecordAsync(
            item,
            Settled(null, "Keep the old flow?", "Yes", new SettledAnswer(SeedIds.Users.Tom, on, AnswerKind.Recommended, null, null), "No"),
            Settled(null, "Which colour?", "Blue", new SettledAnswer(SeedIds.Users.Tom, on, AnswerKind.Alternative, 1, null), "Red", "Green"),
            Settled(null, "Who signs off?", "Priya", new SettledAnswer(SeedIds.Users.Tom, on, AnswerKind.Own, null, "  Mei  ")));

        recorded.Created.ShouldBe([1, 2, 3]);
        await using var connection = await OpenDatabaseAsync();
        var midnight = TestDates.Today.AddDays(-3);
        var decisions = await AskRows.DecisionsAsync(connection, item);
        decisions.Select(d => (d.State, d.CurrentAnswer, d.AnswerKind, d.AlternativeIndex, d.AnsweredByUserId, d.AnsweredAt, d.FromDescription))
            .ShouldBe(
            [
                ("confirmed", "Yes", "recommended", null, SeedIds.Users.Tom, midnight, true),
                ("changed", "Green", "alternative", 1, SeedIds.Users.Tom, midnight, true),
                ("changed", "Mei", "own", null, SeedIds.Users.Tom, midnight, true),
            ]);
        decisions.ShouldAllBe(d => d.CreatedByUserId == SeedIds.Users.Joel && d.CreatedAgentName == "Claude Code (claude.ai)");
        var events = await AskRows.EventsAsync(connection, item);
        events.Where(e => e.Kind == "raised").Select(e => (e.DecisionId, e.ToAnswer, e.ByUserId, e.AgentName, e.FromDescription)).ShouldBe(
            decisions.Select(d => ((Guid?)d.Id, (string?)d.Recommended, (Guid?)SeedIds.Users.Joel, (string?)"Claude Code (claude.ai)", false)), ignoreOrder: true);
        events.Where(e => e.Kind != "raised").Select(e => (e.DecisionId, e.Kind, e.FromAnswer, e.ToAnswer, e.ToKind, e.ByUserId, e.AgentName, e.At, e.FromDescription)).ShouldBe(
            [
                (decisions[0].Id, "confirmed", null, "Yes", "recommended", SeedIds.Users.Tom, null, midnight, true),
                (decisions[1].Id, "changed", "Blue", "Green", "alternative", SeedIds.Users.Tom, null, midnight, true),
                (decisions[2].Id, "changed", "Priya", "Mei", "own", SeedIds.Users.Tom, null, midnight, true),
            ],
            ignoreOrder: true);
    }

    [Test]
    public async Task Record_WhenSettledByNonMember_ShouldFailValidation()
    {
        var item = await NewItemAsync();
        var on = DateOnly.FromDateTime(TestDates.Today.UtcDateTime);

        // Ravi has no membership in SHIP.
        var refused = await Should.ThrowAsync<ApiException>(() => RecordAsync(
            item, Open(null, "Fine?", "Yes"), Settled(null, "Settled?", "Yes", new SettledAnswer(SeedIds.Users.Ravi, on, AnswerKind.Recommended, null, null))));

        refused.Code.ShouldBe(ApiErrorCodes.Validation);
        refused.Field.ShouldBe("decisions");
        await using var connection = await OpenDatabaseAsync();
        (await AskRows.DecisionsAsync(connection, item)).ShouldBeEmpty();
    }

    [Test]
    public async Task Record_WhenSettledAlternativeHasNoIndex_ShouldFailValidation()
    {
        var item = await NewItemAsync();
        var on = DateOnly.FromDateTime(TestDates.Today.UtcDateTime);

        var refused = await Should.ThrowAsync<ApiException>(() => RecordAsync(
            item, Settled(null, "Which?", "A", new SettledAnswer(SeedIds.Users.Joel, on, AnswerKind.Alternative, null, null), "B")));

        refused.Code.ShouldBe(ApiErrorCodes.Validation);
        await using var connection = await OpenDatabaseAsync();
        (await AskRows.DecisionsAsync(connection, item)).ShouldBeEmpty();
    }

    [Test]
    public async Task Record_WhenAgentActs_ShouldStampAgentName()
    {
        var item = await NewItemAsync();
        await using var connection = await OpenDatabaseAsync();
        var (open, _) = await new DecisionBuilder(item).Number(1).Question("Before?").BuildAsync(connection);
        _agent.ActAs("Claude Code", null, false);

        await RecordAsync(item, Open(null, "Raised by the agent?", "Yes"), Open(1, "After?", "Yes"));

        var created = (await AskRows.DecisionsAsync(connection, item)).Single(d => d.Number == 2);
        created.CreatedAgentName.ShouldBe("Claude Code (unverified)");
        var events = await AskRows.EventsAsync(connection, item);
        events.Select(e => (e.DecisionId, e.Kind, e.AgentName)).ShouldBe(
            [(created.Id, "raised", "Claude Code (unverified)"), (open, "edited", "Claude Code (unverified)")], ignoreOrder: true);
    }

    [Test]
    public async Task Record_WhenCommitted_ShouldPublishAsksChanged()
    {
        var item = await NewItemAsync();
        using var watch = new ProjectEventWatch(Host.Services.GetRequiredService<IProjectEvents>(), SeedIds.Projects.Ship);

        await RecordAsync(item, Open(null, "Published?", "Yes"));

        (await watch.NextAsync<AsksChanged>(e => e.WorkItemId == item)).ProjectId.ShouldBe(SeedIds.Projects.Ship);
    }

    [Test]
    public async Task Record_WhenAlternativeEqualsRecommended_ShouldFailValidation()
    {
        var item = await NewItemAsync();

        var refused = await Should.ThrowAsync<ApiException>(() => RecordAsync(item, Open(null, "Same?", "Yes", "No", " Yes ")));

        refused.Code.ShouldBe(ApiErrorCodes.Validation);
        refused.Field.ShouldBe("decisions");
        await using var connection = await OpenDatabaseAsync();
        (await AskRows.DecisionsAsync(connection, item)).ShouldBeEmpty();
    }

    [Test]
    public async Task Record_WhenHighestWasWithdrawn_ShouldNotReuseItsNumber()
    {
        var item = await NewItemAsync();
        await using var connection = await OpenDatabaseAsync();
        await new DecisionBuilder(item).Number(1).BuildAsync(connection);
        await new DecisionBuilder(item).Number(2).State("withdrawn").BuildAsync(connection);

        var recorded = await RecordAsync(item, Open(null, "Next?", "Yes"));

        recorded.Created.ShouldBe([3]);
        (await AskRows.DecisionsAsync(connection, item)).Select(d => (d.Number, d.State)).ShouldBe([(1, "open"), (2, "withdrawn"), (3, "open")]);
    }

    [Test]
    public async Task Record_WhenMixedNumberedAndUnnumbered_ShouldReturnNumbersInInputOrder()
    {
        var item = await NewItemAsync();
        await using var connection = await OpenDatabaseAsync();
        await new DecisionBuilder(item).Number(1).BuildAsync(connection);
        await new DecisionBuilder(item).Number(2).BuildAsync(connection);

        var recorded = await RecordAsync(item, Open(null, "Unnumbered?", "Yes"), Open(4, "Numbered?", "Yes"), Open(2, "Edit two?", "Yes"), Open(1, "Edit one?", "Yes"));

        recorded.Created.ShouldBe([5, 4]);
        recorded.Updated.ShouldBe([2, 1]);
    }

    [Test]
    public async Task Record_WhenNumbersRepeat_ShouldFailValidationNamingTheNumber()
    {
        var item = await NewItemAsync();

        var refused = await RefusedAsync(item, Open(3, "One?", "Yes"), Open(3, "Two?", "Yes"));

        refused.Message.ShouldStartWith("D3: ");
        refused.Message.ShouldContain("can appear once");
        await NothingWrittenAsync(item);
    }

    [TestCase(0)]
    [TestCase(-2)]
    public async Task Record_WhenNumberIsNotPositive_ShouldFailValidationNamingTheNumber(int number)
    {
        var item = await NewItemAsync();

        var refused = await RefusedAsync(item, Open(number, "Zero?", "Yes"));

        refused.Message.ShouldStartWith($"D{number}: ");
        refused.Message.ShouldContain("greater than 0");
        await NothingWrittenAsync(item);
    }

    [Test]
    public async Task Record_WhenQuestionIsOver500Characters_ShouldFailValidation()
    {
        var item = await NewItemAsync();

        var refused = await RefusedAsync(item, Open(null, new string('q', 501), "Yes"));

        refused.Message.ShouldStartWith("Decision 1: ");
        refused.Message.ShouldContain("at most 500 characters");
        await NothingWrittenAsync(item);
    }

    [Test]
    public async Task Record_WhenRecommendedAnswerIsOver500Characters_ShouldFailValidation()
    {
        var item = await NewItemAsync();

        var refused = await RefusedAsync(item, Open(null, "Fine?", "Yes"), Open(null, "Long?", new string('a', 501)));

        refused.Message.ShouldStartWith("Decision 2: ");
        refused.Message.ShouldContain("at most 500 characters");
        await NothingWrittenAsync(item);
    }

    [Test]
    public async Task Record_WhenMoreThanSixAlternatives_ShouldFailValidation()
    {
        var item = await NewItemAsync();

        var refused = await RefusedAsync(item, Open(2, "Many?", "A", "B", "C", "D", "E", "F", "G", "H"));

        refused.Message.ShouldStartWith("D2: ");
        refused.Message.ShouldContain("at most 6 alternatives");
        await NothingWrittenAsync(item);
    }

    [Test]
    public async Task Record_WhenSettledOnAnOpenNumber_ShouldRefuseAndChangeNothing()
    {
        var item = await NewItemAsync();
        await using var connection = await OpenDatabaseAsync();
        var (id, _) = await new DecisionBuilder(item).Number(1).Question("Still open?").BuildAsync(connection);
        var on = DateOnly.FromDateTime(TestDates.Today.UtcDateTime);

        var refused = await RefusedAsync(item, Settled(1, "Now settled?", "Yes", new SettledAnswer(SeedIds.Users.Joel, on, AnswerKind.Recommended, null, null)));

        refused.Message.ShouldStartWith("D1: ");
        refused.Message.ShouldContain("can only be created, not edited");
        var decision = await AskRows.DecisionAsync(connection, id);
        (decision.Question, decision.State).ShouldBe(("Still open?", "open"));
        (await AskRows.EventsAsync(connection, item)).ShouldBeEmpty();
    }

    [Test]
    public async Task Record_WhenSentAgainUnchanged_ShouldWriteNothing()
    {
        var item = await NewItemAsync();
        await RecordAsync(item, Open(null, "Same?", "Yes", "No"));
        await using var connection = await OpenDatabaseAsync();
        var before = await AskRows.DecisionsAsync(connection, item);
        var eventsBefore = await AskRows.EventsAsync(connection, item);

        using var watch = new ProjectEventWatch(Host.Services.GetRequiredService<IProjectEvents>(), SeedIds.Projects.Ship);

        var recorded = await RecordAsync(item, Open(1, "Same?", "Yes", "No"));
        var sentinel = await NewItemAsync();
        await RecordAsync(sentinel, Open(null, "Sentinel?", "Yes"));

        recorded.Created.ShouldBeEmpty();
        recorded.Updated.ShouldBeEmpty();
        (await AskRows.DecisionsAsync(connection, item)).Select(d => (d.Id, d.Question, d.Recommended, d.UpdatedAt))
            .ShouldBe(before.Select(d => (d.Id, d.Question, d.Recommended, d.UpdatedAt)));
        (await AskRows.EventsAsync(connection, item)).Count.ShouldBe(eventsBefore.Count);
        (await watch.NextAsync<AsksChanged>(_ => true)).WorkItemId.ShouldBe(sentinel);
    }

    [TestCase("Yes")]
    [TestCase("  Maybe ")]
    public async Task Record_WhenSettledOwnAnswerRepeatsAnAnswer_ShouldFailValidation(string own)
    {
        var item = await NewItemAsync();
        var on = DateOnly.FromDateTime(TestDates.Today.UtcDateTime);

        var refused = await RefusedAsync(item, Settled(7, "Own?", "Yes", new SettledAnswer(SeedIds.Users.Joel, on, AnswerKind.Own, null, own), "Maybe"));

        refused.Message.ShouldStartWith("D7: ");
        refused.Message.ShouldContain("must differ from the recommended answer and the alternatives");
        await NothingWrittenAsync(item);
    }

    [Test]
    public async Task Record_WhenSettled_ShouldStampTheRaisedEventFromTheClockNotOn()
    {
        var item = await NewItemAsync();
        var on = DateOnly.FromDateTime(TestDates.Today.AddDays(-3).UtcDateTime);
        var before = TimeProvider.System.GetUtcNow();

        await RecordAsync(item, Settled(null, "Old?", "Yes", new SettledAnswer(SeedIds.Users.Tom, on, AnswerKind.Recommended, null, null)));

        var after = TimeProvider.System.GetUtcNow();
        await using var connection = await OpenDatabaseAsync();
        var raised = (await AskRows.EventsAsync(connection, item)).Single(e => e.Kind == "raised");
        raised.At.ShouldBeInRange(before.AddSeconds(-1), after.AddSeconds(1));
        (await AskRows.DecisionsAsync(connection, item)).Single().CreatedAt.ShouldBeInRange(before.AddSeconds(-1), after.AddSeconds(1));
    }

    private async Task<ApiException> RefusedAsync(Guid item, params DecisionRecord[] records)
    {
        var refused = await Should.ThrowAsync<ApiException>(() => RecordAsync(item, records));
        refused.Code.ShouldBe(ApiErrorCodes.Validation);
        refused.Field.ShouldBe("decisions");
        return refused;
    }

    private async Task NothingWrittenAsync(Guid item)
    {
        await using var connection = await OpenDatabaseAsync();
        (await AskRows.DecisionsAsync(connection, item)).ShouldBeEmpty();
        (await AskRows.EventsAsync(connection, item)).ShouldBeEmpty();
    }

    private static DecisionRecord Open(int? number, string question, string recommended, params string[] alternatives)
        => new(number, question, recommended, alternatives, null);

    private static DecisionRecord Settled(int? number, string question, string recommended, SettledAnswer settled, params string[] alternatives)
        => new(number, question, recommended, alternatives, settled);

    private async Task<RecordedDecisions> RecordAsync(Guid item, params DecisionRecord[] records)
    {
        Host.ActAs(SeedIds.Users.Joel);
        try
        {
            return await RecordAsActingUserAsync(item, records);
        }
        finally
        {
            Host.ActAs(null);
        }
    }

    private async Task<RecordedDecisions> RecordAsActingUserAsync(Guid item, params DecisionRecord[] records)
    {
        await using var scope = Host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IDecisionRecorder>().RecordAsync(item, records);
    }

    private async Task<Guid> NewItemAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        return await new WorkItemBuilder(SeedIds.Projects.Ship).InsertAsync(connection);
    }
}
