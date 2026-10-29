using System.Data.Common;
using System.Text.Json;
using ShipLean.Contracts.Boards;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using Shouldly;

namespace ShipLean.TestSupport.Contracts.Boards;

// Behaviour every IBoardSettingsStore has. A derived fixture builds the store in [SetUp] and arranges projects, states and
// cards through the hooks; no test depends on how the implementation stores anything.
[TestFixture]
public abstract class BoardSettingsStoreContract
{
    protected abstract IBoardSettingsStore Store { get; }

    // A project with exactly these states (their ids are the ones given).
    protected abstract Task<Guid> ArrangeProjectAsync(IReadOnlyDictionary<WorkItemTypeId, IReadOnlyList<StateRef>> statesByType);

    // A new live state of the type in the project.
    protected abstract Task ArrangeStateAsync(Guid projectId, WorkItemTypeId type, StateRef state);

    // Archives a state of the project (a state removed in Project settings).
    protected abstract Task ArrangeArchivedStateAsync(Guid projectId, WorkItemTypeId type, Guid stateId);

    // Archives an existing agent connection.
    protected abstract Task ArchiveConnectionAsync(Guid connectionId);

    // An agent connection; archived when asked.
    protected abstract Task<Guid> ArrangeConnectionAsync(bool archived);

    // Makes the column hold at least one card, so it cannot be removed.
    protected abstract Task ArrangeCardInColumnAsync(Guid boardId, string columnId);

    // Runs the action inside a transaction that commits when it returns.
    protected abstract Task InTransactionAsync(Func<DbTransaction, Task> action);

    private async Task<(Guid ProjectId, BoardSettingsDto Board, IReadOnlyDictionary<WorkItemTypeId, IReadOnlyList<StateRef>> States)> ArrangeBoardAsync(BoardLevel level)
    {
        var states = BoardContractSupport.StandardStates();
        var projectId = await ArrangeProjectAsync(states);
        var defaults = Store.BuildDefaults(level, states);
        await InTransactionAsync(transaction => Store.InsertAsync(projectId, level, defaults, transaction));
        var summary = (await Store.ListAsync(projectId)).Single(b => b.Level == level);
        return (projectId, await Store.GetAsync(summary.Id), states);
    }

    [Test]
    public async Task Get_WhenInserted_ShouldReturnWhatWasStored()
    {
        var (projectId, board, _) = await ArrangeBoardAsync(BoardLevel.Stories);

        board.ProjectId.ShouldBe(projectId);
        board.Level.ShouldBe(BoardLevel.Stories);
        board.Settings.Columns.Count.ShouldBeGreaterThan(0);
        board.Warnings.ShouldBeEmpty();
        board.States.Select(s => s.Type).ShouldBe([WorkItemTypeId.Story, WorkItemTypeId.Bug]);
        (await Store.ListAsync(projectId)).ShouldContain(b => b.Id == board.Id && b.Level == BoardLevel.Stories);
    }

    [Test]
    public async Task List_WhenSeveralLevels_ShouldBeInLevelOrder()
    {
        var states = BoardContractSupport.StandardStates();
        var projectId = await ArrangeProjectAsync(states);
        foreach (var level in new[] { BoardLevel.Taskboard, BoardLevel.Epics, BoardLevel.Stories })
        {
            await InTransactionAsync(transaction => Store.InsertAsync(projectId, level, Store.BuildDefaults(level, states), transaction));
        }

        (await Store.ListAsync(projectId)).Select(b => b.Level).ShouldBe([BoardLevel.Stories, BoardLevel.Epics, BoardLevel.Taskboard]);
    }

    [Test]
    public async Task Insert_WhenLevelAlreadyHasABoard_ShouldBeConflict()
    {
        var (projectId, board, states) = await ArrangeBoardAsync(BoardLevel.Features);

        var thrown = await Should.ThrowAsync<ApiException>(
            () => InTransactionAsync(transaction => Store.InsertAsync(projectId, BoardLevel.Features, Store.BuildDefaults(BoardLevel.Features, states), transaction)));

        thrown.Code.ShouldBe(ApiErrorCodes.Conflict);
        board.Level.ShouldBe(BoardLevel.Features);
    }

    [Test]
    public async Task Get_WhenUnknownBoard_ShouldBeNotFound()
    {
        var thrown = await Should.ThrowAsync<ApiException>(() => Store.GetAsync(Guid.NewGuid()));

        thrown.Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Put_WhenUnknownBoard_ShouldBeNotFound()
    {
        var (_, board, _) = await ArrangeBoardAsync(BoardLevel.Stories);

        var thrown = await Should.ThrowAsync<ApiException>(() => Store.PutAsync(Guid.NewGuid(), new BoardSettingsPut(board.Settings, board.Settings)));

        thrown.Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Put_WhenOneSectionChanged_ShouldSaveItAndNameNoSections()
    {
        var (_, board, _) = await ArrangeBoardAsync(BoardLevel.Stories);
        var edited = board.Settings with { TagColours = [new TagColourJson("Urgent", Swatch.Danger)] };

        var result = await Store.PutAsync(board.Id, new BoardSettingsPut(board.Settings, edited));

        result.MergedSections.ShouldBeEmpty();
        BoardContractSupport.Text(result.Board.Settings).ShouldBe(BoardContractSupport.Text(edited));
        BoardContractSupport.Text((await Store.GetAsync(board.Id)).Settings).ShouldBe(BoardContractSupport.Text(edited));
    }

    [Test]
    public async Task Put_WhenStaleOtherSections_ShouldKeepTheirsApplyMine()
    {
        var (_, board, _) = await ArrangeBoardAsync(BoardLevel.Stories);
        var theirs = board.Settings with { TagColours = [new TagColourJson("Urgent", Swatch.Danger)] };
        var mine = board.Settings with { Annotations = new CardAnnotationsJson(false, true, true, false) };
        await Store.PutAsync(board.Id, new BoardSettingsPut(board.Settings, theirs));

        var result = await Store.PutAsync(board.Id, new BoardSettingsPut(board.Settings, mine));

        result.MergedSections.ShouldBe(["tagColours"]);
        BoardContractSupport.Text(result.Board.Settings).ShouldBe(BoardContractSupport.Text(board.Settings with { TagColours = theirs.TagColours, Annotations = mine.Annotations }));
        BoardContractSupport.Text((await Store.GetAsync(board.Id)).Settings).ShouldBe(BoardContractSupport.Text(result.Board.Settings));
    }

    [Test]
    public async Task Put_WhenSameSectionChangedByBoth_ShouldApplyMineAndNotNameIt()
    {
        var (_, board, _) = await ArrangeBoardAsync(BoardLevel.Stories);
        var theirs = board.Settings with { TagColours = [new TagColourJson("Urgent", Swatch.Danger)] };
        var mine = board.Settings with { TagColours = [new TagColourJson("Later", Swatch.Muted)] };
        await Store.PutAsync(board.Id, new BoardSettingsPut(board.Settings, theirs));

        var result = await Store.PutAsync(board.Id, new BoardSettingsPut(board.Settings, mine));

        result.MergedSections.ShouldBeEmpty();
        result.Board.Settings.TagColours.Select(t => t.Tag).ShouldBe(["Later"]);
    }

    [Test]
    public async Task Put_WhenStateUnmapped_ShouldWarnNotFail()
    {
        var (projectId, board, _) = await ArrangeBoardAsync(BoardLevel.Stories);
        var extra = new StateRef(Guid.NewGuid(), "Waiting", StateCategory.InProgress);
        await ArrangeStateAsync(projectId, WorkItemTypeId.Story, extra);
        var edited = board.Settings with { Annotations = new CardAnnotationsJson(true, true, false, false) };

        var result = await Store.PutAsync(board.Id, new BoardSettingsPut(board.Settings, edited));

        var warning = result.Board.Warnings.ShouldHaveSingleItem();
        warning.Kind.ShouldBe(BoardWarningKind.UnmappedState);
        warning.Type.ShouldBe(WorkItemTypeId.Story);
        warning.State.Id.ShouldBe(extra.Id);
        (await Store.GetAsync(board.Id)).Warnings.Select(w => w.State.Id).ShouldBe([extra.Id]);
    }

    [Test]
    public async Task Put_WhenColumnWithCardsRemoved_ShouldBeValidation()
    {
        var (_, board, _) = await ArrangeBoardAsync(BoardLevel.Stories);
        var removed = board.Settings.Columns[1];
        await ArrangeCardInColumnAsync(board.Id, removed.Id);
        var edited = board.Settings with { Columns = board.Settings.Columns.Where(c => c.Id != removed.Id).ToList() };

        var thrown = await Should.ThrowAsync<ApiException>(() => Store.PutAsync(board.Id, new BoardSettingsPut(board.Settings, edited)));

        thrown.Code.ShouldBe(ApiErrorCodes.Validation);
        (await Store.GetAsync(board.Id)).Settings.Columns.Select(c => c.Id).ShouldContain(removed.Id);
    }

    [Test]
    public async Task Put_WhenEmptyColumnRemoved_ShouldSave()
    {
        var (_, board, _) = await ArrangeBoardAsync(BoardLevel.Stories);
        var removed = board.Settings.Columns[1];
        var edited = board.Settings with { Columns = board.Settings.Columns.Where(c => c.Id != removed.Id).ToList() };

        var result = await Store.PutAsync(board.Id, new BoardSettingsPut(board.Settings, edited));

        result.Board.Settings.Columns.Select(c => c.Id).ShouldNotContain(removed.Id);
    }

    [TestCase(BoardLevel.Stories)]
    [TestCase(BoardLevel.Features)]
    [TestCase(BoardLevel.Epics)]
    public void BuildDefaults_ShouldMapEveryNonRemovedState(BoardLevel level)
    {
        var states = BoardContractSupport.StandardStates();

        var defaults = Store.BuildDefaults(level, states);

        var shown = level == BoardLevel.Stories ? new[] { WorkItemTypeId.Story, WorkItemTypeId.Bug } : [PrimaryType(level)];
        foreach (var type in shown)
        {
            var live = states[type].Where(s => s.Category != StateCategory.Removed).Select(s => s.Id);
            defaults.Columns.SelectMany(c => c.StateMappings).Where(m => m.Type == type).Select(m => m.StateId).Distinct().ShouldBe(live, ignoreOrder: true);
            defaults.Columns.ShouldAllBe(c => c.StateMappings.Count(m => m.Type == type) == 1);
        }
        defaults.Columns.ShouldAllBe(c => c.StateMappings.Count == shown.Length);
    }

    [Test]
    public void BuildDefaults_WhenStoriesBoard_ShouldHaveOneColumnPerStoryStateAndMapBugsByCategory()
    {
        var states = BoardContractSupport.StandardStates();

        var defaults = Store.BuildDefaults(BoardLevel.Stories, states);

        defaults.Columns.Select(c => c.Name).ShouldBe(["New", "Active", "Resolved", "Closed"]);
        defaults.Columns.Select(c => c.Id).Distinct().Count().ShouldBe(4);
        foreach (var (column, index) in defaults.Columns.Select((c, i) => (c, i)))
        {
            column.StateMappings.Single(m => m.Type == WorkItemTypeId.Story).StateId.ShouldBe(states[WorkItemTypeId.Story][index].Id);
            column.StateMappings.Single(m => m.Type == WorkItemTypeId.Bug).StateId.ShouldBe(states[WorkItemTypeId.Bug][index].Id);
        }
        defaults.Columns.ShouldAllBe(c => c.WipLimit == null && !c.Split && c.DefinitionOfDone == string.Empty);
        defaults.Swimlanes.ShouldHaveSingleItem().Kind.ShouldBe(SwimlaneKind.Default);
        defaults.StyleRules.ShouldBeEmpty();
        defaults.TagColours.ShouldBeEmpty();
        defaults.CardReordering.ShouldBe(CardReordering.ChangesBacklogOrder);
        defaults.CardFields.Select(f => f.Type).ShouldBe([WorkItemTypeId.Story, WorkItemTypeId.Bug]);
        defaults.Annotations.Tasks.ShouldBeTrue();
    }

    [Test]
    public void BuildDefaults_WhenTaskboard_ShouldBeEmpty()
    {
        var defaults = Store.BuildDefaults(BoardLevel.Taskboard, BoardContractSupport.StandardStates());

        defaults.Columns.ShouldBeEmpty();
        defaults.Swimlanes.ShouldBeEmpty();
        defaults.CardReordering.ShouldBeNull();
        defaults.CardFields.Select(f => f.Type).ShouldBe([WorkItemTypeId.Task, WorkItemTypeId.Story, WorkItemTypeId.Bug]);
    }

    [Test]
    public void BuildDefaults_WhenStatesOutOfCategoryOrder_ShouldOrderColumnsByCategoryAndSkipRemoved()
    {
        var states = BoardContractSupport.StandardStates().ToDictionary(p => p.Key, p => p.Value);
        states[WorkItemTypeId.Feature] = [.. states[WorkItemTypeId.Feature].Reverse()];

        var defaults = Store.BuildDefaults(BoardLevel.Features, states);

        defaults.Columns.Select(c => c.Name).ShouldBe(["New", "Active", "Resolved", "Closed"]);
    }

    [TestCase(BoardLevel.Stories)]
    [TestCase(BoardLevel.Features)]
    [TestCase(BoardLevel.Epics)]
    [TestCase(BoardLevel.Taskboard)]
    public void BuildDefaults_ShouldSurviveThePersistedJsonRoundTrip(BoardLevel level)
    {
        var defaults = Store.BuildDefaults(level, BoardContractSupport.StandardStates());

        var text = BoardContractSupport.Text(defaults);

        BoardContractSupport.Text(System.Text.Json.JsonSerializer.Deserialize<BoardSettingsJson>(text, PersistedJson.Options)!).ShouldBe(text);
    }

    [Test]
    public async Task BuildDefaults_WhenShipStates_ShouldMatchSeedColumns()
    {
        var states = BoardContractSupport.SeedStates();
        var story = states[WorkItemTypeId.Story];
        var bug = states[WorkItemTypeId.Bug];

        var defaults = Store.BuildDefaults(BoardLevel.Stories, states);

        defaults.Columns.Select(c => c.Name).ShouldBe(["New", "Triaged", "Active", "Resolved", "Closed"]);
        defaults.Columns.Select(c => c.Id).ShouldBe(["col-new", "col-triaged", "col-active", "col-resolved", "col-closed"]);
        var storyIds = defaults.Columns.Select(c => c.StateMappings.Single(m => m.Type == WorkItemTypeId.Story).StateId).ToList();
        var bugIds = defaults.Columns.Select(c => c.StateMappings.Single(m => m.Type == WorkItemTypeId.Bug).StateId).ToList();
        storyIds.ShouldBe([story[0].Id, story[0].Id, story[1].Id, story[2].Id, story[3].Id]);
        bugIds.ShouldBe([bug[0].Id, bug[1].Id, bug[2].Id, bug[3].Id, bug[4].Id]);
        defaults.Columns.SelectMany(c => c.StateMappings).ShouldNotContain(m => m.StateId == Guid.Empty);
        var projectId = await ArrangeProjectAsync(states);
        await InTransactionAsync(transaction => Store.InsertAsync(projectId, BoardLevel.Stories, defaults, transaction));
        var summary = (await Store.ListAsync(projectId)).Single();
        (await Store.GetAsync(summary.Id)).Warnings.ShouldBeEmpty();
    }

    [Test]
    public void BuildDefaults_WhenShownTypeHasNoStates_ShouldThrow()
    {
        var states = BoardContractSupport.StandardStates().ToDictionary(p => p.Key, p => p.Value);
        states[WorkItemTypeId.Bug] = states[WorkItemTypeId.Bug].Where(s => s.Category == StateCategory.Removed).ToList();
        var missing = new Dictionary<WorkItemTypeId, IReadOnlyList<StateRef>>(states);
        missing.Remove(WorkItemTypeId.Feature);

        Should.Throw<ArgumentException>(() => Store.BuildDefaults(BoardLevel.Stories, states));
        Should.Throw<ArgumentException>(() => Store.BuildDefaults(BoardLevel.Features, missing));
    }

    [Test]
    public void BuildDefaults_WhenSecondaryTypeHasExtraStateOfACategoryTheOthersLack_ShouldNeverLeaveAStateUnmapped()
    {
        var states = BoardContractSupport.StandardStates().ToDictionary(p => p.Key, p => p.Value);
        var story = states[WorkItemTypeId.Story].Where(s => s.Category != StateCategory.Resolved).ToList();
        states[WorkItemTypeId.Story] = story;

        var defaults = Store.BuildDefaults(BoardLevel.Stories, states);

        defaults.Columns.Select(c => c.Name).ShouldBe(["New", "Active", "Resolved", "Closed"]);
        defaults.Columns.SelectMany(c => c.StateMappings).Select(m => m.StateId).ShouldNotContain(Guid.Empty);
        defaults.Columns[2].StateMappings.Single(m => m.Type == WorkItemTypeId.Story).StateId.ShouldBe(story[1].Id);
    }

    private async Task<(BoardSettingsDto Board, Guid ProjectId)> ArrangeStoriesAsync()
    {
        var (projectId, board, _) = await ArrangeBoardAsync(BoardLevel.Stories);
        return (board, projectId);
    }

    // The error a put fails with; the stored document is untouched.
    private async Task<string> PutFailureAsync(BoardSettingsDto board, BoardSettingsJson edited)
    {
        var thrown = await Should.ThrowAsync<ApiException>(() => Store.PutAsync(board.Id, new BoardSettingsPut(board.Settings, edited)));

        BoardContractSupport.Text((await Store.GetAsync(board.Id)).Settings).ShouldBe(BoardContractSupport.Text(board.Settings));
        return thrown.Code;
    }

    [Test]
    public async Task Put_WhenLaneCriteriaInvalid_ShouldBeValidation()
    {
        var (board, _) = await ArrangeStoriesAsync();
        var bad = new SwimlaneJson("lane-bad", "Bad", null, SwimlaneKind.Criteria, Criteria("colour", CriteriaOp.Equal, "red"));

        (await PutFailureAsync(board, board.Settings with { Swimlanes = [bad, .. board.Settings.Swimlanes] })).ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Put_WhenStyleRuleCriteriaInvalid_ShouldBeValidation()
    {
        var (board, _) = await ArrangeStoriesAsync();
        var rule = new StyleRuleJson("rule-bad", "Bad", true, Swatch.Danger, false, false, false, Criteria(FieldIds.Priority, CriteriaOp.Contains, "1"));

        (await PutFailureAsync(board, board.Settings with { StyleRules = [rule] })).ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Put_WhenNoDefaultLane_ShouldBeValidation()
    {
        var (board, _) = await ArrangeStoriesAsync();

        (await PutFailureAsync(board, board.Settings with { Swimlanes = board.Settings.Swimlanes.Where(l => l.Kind != SwimlaneKind.Default).ToList() })).ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Put_WhenTwoDefaultLanes_ShouldBeValidation()
    {
        var (board, _) = await ArrangeStoriesAsync();
        var second = board.Settings.Swimlanes[0] with { Id = "lane-second" };

        (await PutFailureAsync(board, board.Settings with { Swimlanes = [.. board.Settings.Swimlanes, second] })).ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Put_WhenMappingStateOfOtherType_ShouldBeValidation()
    {
        var (board, _) = await ArrangeStoriesAsync();
        var bugState = board.States.Single(s => s.Type == WorkItemTypeId.Bug).States.First(s => s.Category != StateCategory.Removed);
        var first = board.Settings.Columns[1];
        var wrong = first with { StateMappings = first.StateMappings.Select(m => m.Type == WorkItemTypeId.Story ? m with { StateId = bugState.Id } : m).ToList() };

        (await PutFailureAsync(board, board.Settings with { Columns = Replace(board.Settings.Columns, 1, wrong) })).ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Put_WhenMappingRemovedState_ShouldBeValidation()
    {
        var (board, _) = await ArrangeStoriesAsync();
        var removed = board.States.Single(s => s.Type == WorkItemTypeId.Story).States.Single(s => s.Category == StateCategory.Removed);
        var column = board.Settings.Columns[1];
        var wrong = column with { StateMappings = column.StateMappings.Select(m => m.Type == WorkItemTypeId.Story ? m with { StateId = removed.Id } : m).ToList() };

        (await PutFailureAsync(board, board.Settings with { Columns = Replace(board.Settings.Columns, 1, wrong) })).ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Put_WhenColumnLacksAMappingForAShownType_ShouldBeValidation()
    {
        var (board, _) = await ArrangeStoriesAsync();
        var column = board.Settings.Columns[1];
        var wrong = column with { StateMappings = column.StateMappings.Where(m => m.Type == WorkItemTypeId.Story).ToList() };

        (await PutFailureAsync(board, board.Settings with { Columns = Replace(board.Settings.Columns, 1, wrong) })).ShouldBe(ApiErrorCodes.Validation);
    }

    [TestCase(0)]
    [TestCase(3)]
    public async Task Put_WhenFirstOrLastColumnHasAWipLimitOrSplit_ShouldBeValidation(int index)
    {
        var (board, _) = await ArrangeStoriesAsync();

        (await PutFailureAsync(board, board.Settings with { Columns = Replace(board.Settings.Columns, index, board.Settings.Columns[index] with { WipLimit = 3 }) })).ShouldBe(ApiErrorCodes.Validation);
        (await PutFailureAsync(board, board.Settings with { Columns = Replace(board.Settings.Columns, index, board.Settings.Columns[index] with { Split = true }) })).ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Put_WhenAMiddleColumnHasAWipLimit_ShouldSave()
    {
        var (board, _) = await ArrangeStoriesAsync();

        var result = await Store.PutAsync(board.Id, new BoardSettingsPut(board.Settings, board.Settings with { Columns = Replace(board.Settings.Columns, 1, board.Settings.Columns[1] with { WipLimit = 3, Split = true }) }));

        result.Board.Settings.Columns[1].WipLimit.ShouldBe(3);
    }

    [Test]
    public async Task Put_WhenTooManyOrInapplicableExtraFields_ShouldBeValidation()
    {
        var (board, _) = await ArrangeStoriesAsync();
        var story = board.Settings.CardFields.Single(f => f.Type == WorkItemTypeId.Story);
        var many = story with { ExtraFields = Enumerable.Repeat(FieldIds.Priority, 11).ToList() };
        var notForStories = story with { ExtraFields = [FieldIds.Severity] };
        var notACardField = story with { ExtraFields = [FieldIds.Title] };

        foreach (var wrong in new[] { many, notForStories, notACardField })
        {
            (await PutFailureAsync(board, board.Settings with { CardFields = board.Settings.CardFields.Select(f => f.Type == WorkItemTypeId.Story ? wrong : f).ToList() })).ShouldBe(ApiErrorCodes.Validation);
        }
    }

    private static StateRefJson Ref(StateRef state) => new(state.Id, state.Name);

    private static TransitionJson Transition(string id, WorkItemTypeId type, StateRef? from, StateRef to, Guid? connectionId = null, string prompt = "Do it.")
        => new(id, type, from is null ? null : Ref(from), Ref(to), [new ActionJson(id + "-a", "agent", prompt, connectionId)]);

    private static List<StateRef> Live(BoardSettingsDto board, WorkItemTypeId type)
        => board.States.Single(s => s.Type == type).States.Where(s => s.Category != StateCategory.Removed).ToList();

    [Test]
    public async Task Put_WhenTransitionsAreValid_ShouldSaveAndReturnThem()
    {
        var (board, _) = await ArrangeStoriesAsync();
        var story = Live(board, WorkItemTypeId.Story);
        var bug = Live(board, WorkItemTypeId.Bug);
        var edited = board.Settings with { Transitions = [Transition("t1", WorkItemTypeId.Story, story[0], story[1]), Transition("t2", WorkItemTypeId.Bug, null, bug[1])] };

        var result = await Store.PutAsync(board.Id, new BoardSettingsPut(board.Settings, edited));

        BoardContractSupport.Text(result.Board.Settings).ShouldBe(BoardContractSupport.Text(edited));
        BoardContractSupport.Text((await Store.GetAsync(board.Id)).Settings).ShouldBe(BoardContractSupport.Text(edited));
    }

    [Test]
    public async Task Put_WhenTransitionsMissing_ShouldKeepTheStoredOnes()
    {
        var (board, _) = await ArrangeStoriesAsync();
        var story = Live(board, WorkItemTypeId.Story);
        var saved = board.Settings with { Transitions = [Transition("t1", WorkItemTypeId.Story, story[0], story[1])] };
        await Store.PutAsync(board.Id, new BoardSettingsPut(board.Settings, saved));
        var old = board.Settings with { Transitions = null! };

        var result = await Store.PutAsync(board.Id, new BoardSettingsPut(old, old with { TagColours = [new TagColourJson("Urgent", Swatch.Danger)] }));

        result.Board.Settings.Transitions.Select(t => t.Id).ShouldBe(["t1"]);
        result.Board.Settings.TagColours.Count.ShouldBe(1);
    }

    [Test]
    public async Task Put_WhenTransitionGoesFromAStateToItself_ShouldBeValidation()
    {
        var (board, _) = await ArrangeStoriesAsync();
        var story = Live(board, WorkItemTypeId.Story);

        (await PutFailureAsync(board, board.Settings with { Transitions = [Transition("t1", WorkItemTypeId.Story, story[1], story[1])] })).ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Put_WhenTransitionIsListedTwice_ShouldBeValidation()
    {
        var (board, _) = await ArrangeStoriesAsync();
        var story = Live(board, WorkItemTypeId.Story);
        var twice = new[] { Transition("t1", WorkItemTypeId.Story, null, story[1]), Transition("t2", WorkItemTypeId.Story, null, story[1]) };

        (await PutFailureAsync(board, board.Settings with { Transitions = twice })).ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Put_WhenTransitionTypeIsNotOnTheBoard_ShouldBeValidation()
    {
        var (board, _) = await ArrangeStoriesAsync();
        var feature = BoardContractSupport.StandardStates()[WorkItemTypeId.Feature];

        (await PutFailureAsync(board, board.Settings with { Transitions = [Transition("t1", WorkItemTypeId.Feature, null, feature[1])] })).ShouldBe(ApiErrorCodes.Validation);
    }

    [TestCase(BoardLevel.Epics, WorkItemTypeId.Epic)]
    [TestCase(BoardLevel.Taskboard, WorkItemTypeId.Task)]
    public async Task Put_WhenBoardHasNoTransitions_ShouldBeValidation(BoardLevel level, WorkItemTypeId type)
    {
        var (_, board, states) = await ArrangeBoardAsync(level);

        (await PutFailureAsync(board, board.Settings with { Transitions = [Transition("t1", type, null, states[type][1])] })).ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Put_WhenPromptIsTooLong_ShouldBeValidation()
    {
        var (board, _) = await ArrangeStoriesAsync();
        var story = Live(board, WorkItemTypeId.Story);
        var exactly = board.Settings with { Transitions = [Transition("t1", WorkItemTypeId.Story, null, story[1], prompt: new string('x', 20_000))] };
        var over = board.Settings with { Transitions = [Transition("t1", WorkItemTypeId.Story, null, story[1], prompt: new string('x', 20_001))] };

        (await PutFailureAsync(board, over)).ShouldBe(ApiErrorCodes.Validation);
        (await Store.PutAsync(board.Id, new BoardSettingsPut(board.Settings, exactly))).Board.Settings.Transitions.Count.ShouldBe(1);
    }

    [Test]
    public async Task Put_WhenTransitionHasNoActions_ShouldBeValidation()
    {
        var (board, _) = await ArrangeStoriesAsync();
        var story = Live(board, WorkItemTypeId.Story);

        (await PutFailureAsync(board, board.Settings with { Transitions = [Transition("t1", WorkItemTypeId.Story, null, story[1]) with { Actions = [] }] })).ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Put_WhenNewTransitionNamesAnArchivedState_ShouldBeValidation()
    {
        var (board, projectId) = await ArrangeStoriesAsync();
        var spare = new StateRef(Guid.NewGuid(), "Spare", StateCategory.Proposed);
        await ArrangeStateAsync(projectId, WorkItemTypeId.Story, spare);
        await ArrangeArchivedStateAsync(projectId, WorkItemTypeId.Story, spare.Id);
        var story = Live(board, WorkItemTypeId.Story);

        (await PutFailureAsync(board, board.Settings with { Transitions = [Transition("t1", WorkItemTypeId.Story, story[0], spare)] })).ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Put_WhenSavingColumnsWhileAnUnchangedInvalidTransitionExists_ShouldSave()
    {
        var (board, projectId) = await ArrangeStoriesAsync();
        var spare = new StateRef(Guid.NewGuid(), "Spare", StateCategory.Proposed);
        await ArrangeStateAsync(projectId, WorkItemTypeId.Story, spare);
        var story = Live(board, WorkItemTypeId.Story);
        var withTransition = (await Store.PutAsync(board.Id, new BoardSettingsPut(board.Settings, board.Settings with { Transitions = [Transition("t1", WorkItemTypeId.Story, story[0], spare)] }))).Board;
        await ArrangeArchivedStateAsync(projectId, WorkItemTypeId.Story, spare.Id);
        var edited = withTransition.Settings with { Columns = Replace(withTransition.Settings.Columns, 1, withTransition.Settings.Columns[1] with { WipLimit = 4 }) };

        var result = await Store.PutAsync(board.Id, new BoardSettingsPut(withTransition.Settings, edited));

        result.Board.Settings.Columns[1].WipLimit.ShouldBe(4);
        result.Board.Settings.Transitions.Single().To.StateId.ShouldBe(spare.Id);
    }

    [Test]
    public async Task Put_WhenAStateWasReAddedUnderTheSameName_ShouldRebindTheTransition()
    {
        var (board, projectId) = await ArrangeStoriesAsync();
        var spare = new StateRef(Guid.NewGuid(), "Spare", StateCategory.Proposed);
        await ArrangeStateAsync(projectId, WorkItemTypeId.Story, spare);
        var story = Live(board, WorkItemTypeId.Story);
        var saved = (await Store.PutAsync(board.Id, new BoardSettingsPut(board.Settings, board.Settings with { Transitions = [Transition("t1", WorkItemTypeId.Story, story[0], spare)] }))).Board;
        await ArrangeArchivedStateAsync(projectId, WorkItemTypeId.Story, spare.Id);
        var back = new StateRef(Guid.NewGuid(), "SPARE", StateCategory.Proposed);
        await ArrangeStateAsync(projectId, WorkItemTypeId.Story, back);

        var result = await Store.PutAsync(board.Id, new BoardSettingsPut(saved.Settings, saved.Settings with { TagColours = [new TagColourJson("Urgent", Swatch.Danger)] }));

        var transition = result.Board.Settings.Transitions.Single();
        transition.To.ShouldBe(new StateRefJson(back.Id, "SPARE"));
        transition.Id.ShouldBe("t1");
    }

    [Test]
    public async Task Put_WhenActionNamesAnArchivedOrUnknownConnection_ShouldBeValidation()
    {
        var (board, _) = await ArrangeStoriesAsync();
        var story = Live(board, WorkItemTypeId.Story);
        var archived = await ArrangeConnectionAsync(archived: true);

        (await PutFailureAsync(board, board.Settings with { Transitions = [Transition("t1", WorkItemTypeId.Story, null, story[1], archived)] })).ShouldBe(ApiErrorCodes.Validation);
        (await PutFailureAsync(board, board.Settings with { Transitions = [Transition("t1", WorkItemTypeId.Story, null, story[1], Guid.NewGuid())] })).ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Put_WhenAnActionKeepsAConnectionThatWasArchivedAfterSaving_ShouldSave()
    {
        var (board, _) = await ArrangeStoriesAsync();
        var story = Live(board, WorkItemTypeId.Story);
        var connection = await ArrangeConnectionAsync(archived: false);
        var saved = (await Store.PutAsync(board.Id, new BoardSettingsPut(board.Settings, board.Settings with { Transitions = [Transition("t1", WorkItemTypeId.Story, null, story[1], connection)] }))).Board;
        await ArchiveConnectionAsync(connection);

        var result = await Store.PutAsync(board.Id, new BoardSettingsPut(saved.Settings, saved.Settings with { TagColours = [new TagColourJson("Urgent", Swatch.Danger)] }));

        result.Board.Settings.Transitions.Single().Actions.Single().ConnectionId.ShouldBe(connection);
    }

    private static List<BoardColumnJson> Replace(IReadOnlyList<BoardColumnJson> columns, int index, BoardColumnJson column)
        => columns.Select((c, i) => i == index ? column : c).ToList();

    private static CriteriaJson Criteria(string field, CriteriaOp op, string value)
        => new(CriteriaMatch.All, [new CriteriaClause(field, op, JsonSerializer.SerializeToElement(value))]);

    private static WorkItemTypeId PrimaryType(BoardLevel level) => level == BoardLevel.Features ? WorkItemTypeId.Feature : WorkItemTypeId.Epic;
}
