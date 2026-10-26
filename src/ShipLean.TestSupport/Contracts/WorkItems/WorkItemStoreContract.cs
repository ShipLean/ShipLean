using ShipLean.Application.Fakes.WorkItems;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport;
using Shouldly;
using static ShipLean.TestSupport.Contracts.WorkItems.WorkItemContractSupport;

namespace ShipLean.TestSupport.Contracts.WorkItems;

// Behaviour every IWorkItemStore must have, whatever it stores data in.
[TestFixture]
public abstract class WorkItemStoreContract
{
    // CreateStore is called first in each test, then ArrangeProjectAsync (the fake arranges the store it was just given;
    // Postgres derivations arrange rows).
    protected abstract IWorkItemStore CreateStore(IWorkItemRules rules, Guid actorId);

    // Returns a new project (its own key) on every call, with its own states (one per category for each type), a board
    // holding the columns ArrangedProject.ColumnId names, and an import job of its own.
    protected abstract Task<ArrangedProject> ArrangeProjectAsync();

    [Test]
    public async Task Create_ShouldGiveTheNextKeyAndTheFirstProposedState()
    {
        var store = CreateStore(new PassThroughWorkItemRules(), Actor);
        var project = await ArrangeProjectAsync();

        var first = await CreateAsync(store, project, WorkItemTypeId.Story, "First");
        var second = await CreateAsync(store, project, WorkItemTypeId.Story, "Second");

        var firstItem = await store.GetAsync(first.Id);
        var secondItem = await store.GetAsync(second.Id);
        secondItem.Number.ShouldBe(firstItem.Number + 1);
        second.Key.ShouldBe($"{project.Key}-{secondItem.Number}");
        secondItem.StateId.ShouldBe(project.StateId(WorkItemTypeId.Story, StateCategory.Proposed));
        secondItem.Revision.ShouldBe(1);
        secondItem.CreatedBy.ShouldBe(Actor);
    }

    [Test]
    public async Task Create_WhenStoryParentIsEpic_ShouldBeValidation()
    {
        var store = CreateStore(new PassThroughWorkItemRules(), Actor);
        var project = await ArrangeProjectAsync();
        var epic = await CreateAsync(store, project, WorkItemTypeId.Epic, "Epic");

        var error = await Should.ThrowAsync<ApiException>(() => CreateAsync(store, project, WorkItemTypeId.Story, "Story", epic.Id));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Create_WhenStoryParentIsFeature_ShouldSucceed()
    {
        var store = CreateStore(new PassThroughWorkItemRules(), Actor);
        var project = await ArrangeProjectAsync();
        var feature = await CreateAsync(store, project, WorkItemTypeId.Feature, "Feature");

        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Story", feature.Id);

        (await store.GetAsync(story.Id)).ParentId.ShouldBe(feature.Id);
    }

    [Test]
    public async Task Create_WhenNoParent_ShouldSucceed()
    {
        var store = CreateStore(new PassThroughWorkItemRules(), Actor);
        var project = await ArrangeProjectAsync();

        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Orphan");

        (await store.GetAsync(story.Id)).ParentId.ShouldBeNull();
    }

    [Test]
    public async Task Patch_ShouldWriteOneRevisionWithTheChangedFieldsOnly()
    {
        var store = CreateStore(new PassThroughWorkItemRules(), Actor);
        var project = await ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Before");

        var changes = new WorkItemChanges { Title = new("After"), Points = new(5m), Description = new("Same"), Priority = new(2) };
        var result = Written(await store.PatchAsync(story.Id, new WorkItemPatch(1, changes), WriteOptions.User));

        result.Revision.ShouldBe(2);
        result.Changes.Select(c => c.Field).ShouldBe(["title", "points", "description"]);
        var revisions = await store.GetRevisionsAsync(story.Id);
        revisions.Count.ShouldBe(2);
        revisions[1].Rev.ShouldBe(2);
        revisions[1].ChangedBy.ShouldBe(Actor);
        revisions[1].Changes.Select(c => c.Field).ShouldBe(["title", "points", "description"]);
    }

    [Test]
    public async Task Patch_WhenStaleRevision_ShouldReturnTheirChanges()
    {
        var store = CreateStore(new PassThroughWorkItemRules(), Actor);
        var project = await ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Original");
        Written(await store.PatchAsync(story.Id, new WorkItemPatch(1, new WorkItemChanges { Title = new("Theirs") }), WriteOptions.User));

        var outcome = await store.PatchAsync(story.Id, new WorkItemPatch(1, new WorkItemChanges { Title = new("Mine") }), WriteOptions.User);

        outcome.Written.ShouldBeNull();
        var conflict = outcome.Conflict.ShouldNotBeNull();
        conflict.Code.ShouldBe(ApiErrorCodes.Conflict);
        conflict.Error.ShouldNotBeNullOrWhiteSpace();
        conflict.Current.Revision.ShouldBe(2);
        conflict.Current.Title.ShouldBe("Theirs");
        conflict.TheirChanges.Select(r => r.Rev).ShouldBe([2]);
        conflict.TheirChanges[0].Changes.Select(c => c.Field).ShouldBe(["title"]);
        (await store.GetAsync(story.Id)).Title.ShouldBe("Theirs");
    }

    [Test]
    public async Task PatchMany_WhenOneFails_ShouldWriteNothing()
    {
        var store = CreateStore(new PassThroughWorkItemRules(), Actor);
        var project = await ArrangeProjectAsync();
        var first = await CreateAsync(store, project, WorkItemTypeId.Story, "First");
        var second = await CreateAsync(store, project, WorkItemTypeId.Story, "Second");

        var error = await Should.ThrowAsync<ApiException>(() => store.PatchManyAsync(
            [
                (first.Id, new WorkItemPatch(1, new WorkItemChanges { Title = new("Changed") })),
                (second.Id, new WorkItemPatch(1, new WorkItemChanges { Priority = new(9) })),
            ],
            WriteOptions.User));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
        var unchanged = await store.GetAsync(first.Id);
        unchanged.Title.ShouldBe("First");
        unchanged.Revision.ShouldBe(1);
    }

    [Test]
    public async Task PatchMany_WhenAllSucceed_ShouldWriteEachItem()
    {
        var store = CreateStore(new PassThroughWorkItemRules(), Actor);
        var project = await ArrangeProjectAsync();
        var first = await CreateAsync(store, project, WorkItemTypeId.Story, "First");
        var second = await CreateAsync(store, project, WorkItemTypeId.Story, "Second");

        var outcome = await store.PatchManyAsync(
            [
                (first.Id, new WorkItemPatch(1, new WorkItemChanges { Points = new(1m) })),
                (second.Id, new WorkItemPatch(1, new WorkItemChanges { Points = new(2m) })),
            ],
            WriteOptions.User);

        outcome.Conflicts.ShouldBeNull();
        outcome.Written.ShouldNotBeNull().Select(w => w.Revision).ShouldBe([2, 2]);
    }

    [Test]
    public async Task Move_WhenSetGiven_ShouldWriteOneRevision()
    {
        var store = CreateStore(new PassThroughWorkItemRules(), Actor);
        var project = await ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Card");

        var target = new MoveTarget(1, project.ColumnId(1), false, null, null, new WorkItemChanges { Points = new(8m) });
        var result = Written(await store.MoveAsync(story.Id, target, WriteOptions.User));

        result.Revision.ShouldBe(2);
        (await store.GetRevisionsAsync(story.Id)).Count.ShouldBe(2);
        var moved = await store.GetAsync(story.Id);
        moved.ColumnId.ShouldBe(project.ColumnId(1));
        moved.Points.ShouldBe(8m);
    }

    [Test]
    public async Task Move_WhenStale_ShouldReturnAConflict()
    {
        var store = CreateStore(new PassThroughWorkItemRules(), Actor);
        var project = await ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Card");
        Written(await store.PatchAsync(story.Id, new WorkItemPatch(1, new WorkItemChanges { Points = new(3m) }), WriteOptions.User));

        var outcome = await store.MoveAsync(story.Id, new MoveTarget(1, project.ColumnId(1), false, null, null, null), WriteOptions.User);

        outcome.Written.ShouldBeNull();
        outcome.Conflict.ShouldNotBeNull().TheirChanges.Count.ShouldBe(1);
        (await store.GetAsync(story.Id)).ColumnId.ShouldNotBe(project.ColumnId(1));
    }

    [Test]
    public async Task Create_WhenRulesSetAColumn_ShouldPlaceItLastInIt()
    {
        string? column = null;
        var store = CreateStore(new RecordingWorkItemRules(change => column is null ? change : change with { ColumnId = column }), Actor);
        var project = await ArrangeProjectAsync();
        column = project.ColumnId(1);

        var first = await CreateAsync(store, project, WorkItemTypeId.Story, "First");
        var second = await CreateAsync(store, project, WorkItemTypeId.Story, "Second");

        var a = await store.GetAsync(first.Id);
        var b = await store.GetAsync(second.Id);
        a.ColumnId.ShouldBe(column);
        b.ColumnId.ShouldBe(column);
        a.ColumnOrder.ShouldNotBeNull();
        b.ColumnOrder.ShouldNotBeNull();
        string.CompareOrdinal(a.ColumnOrder, b.ColumnOrder).ShouldBeLessThan(0);
    }

    [Test]
    public async Task Patch_WhenRulesChangeTheColumn_ShouldPutItLastInTheNewColumn()
    {
        string? column = null;
        var store = CreateStore(new RecordingWorkItemRules(change => column is null ? change : change with { ColumnId = column }), Actor);
        var project = await ArrangeProjectAsync();
        column = project.ColumnId(1);
        var a = await CreateAsync(store, project, WorkItemTypeId.Story, "A");
        var b = await CreateAsync(store, project, WorkItemTypeId.Story, "B");
        column = project.ColumnId(2);
        var c = await CreateAsync(store, project, WorkItemTypeId.Story, "C");
        column = project.ColumnId(1);

        Written(await store.PatchAsync(c.Id, new WorkItemPatch(c.Revision, new WorkItemChanges { Title = new("C moved") }), WriteOptions.User));

        var moved = await store.GetAsync(c.Id);
        moved.ColumnId.ShouldBe(project.ColumnId(1));
        string.CompareOrdinal((await store.GetAsync(a.Id)).ColumnOrder, moved.ColumnOrder).ShouldBeLessThan(0);
        string.CompareOrdinal((await store.GetAsync(b.Id)).ColumnOrder, moved.ColumnOrder).ShouldBeLessThan(0);
    }

    [Test]
    public async Task Reorder_ShouldNotCallTheRules()
    {
        var rules = new RecordingWorkItemRules();
        var store = CreateStore(rules, Actor);
        var project = await ArrangeProjectAsync();
        var a = await CreateAsync(store, project, WorkItemTypeId.Story, "A");
        var b = await CreateAsync(store, project, WorkItemTypeId.Story, "B");
        var callsBefore = rules.Calls.Count;

        Written(await store.ReorderAsync(a.Id, a.Revision, b.Id));

        rules.Calls.Count.ShouldBe(callsBefore);
    }

    [Test]
    public async Task Reorder_WhenBeforeGiven_ShouldPlaceItBeforeThatItem()
    {
        var store = CreateStore(new PassThroughWorkItemRules(), Actor);
        var project = await ArrangeProjectAsync();
        var a = await CreateAsync(store, project, WorkItemTypeId.Story, "A");
        var b = await CreateAsync(store, project, WorkItemTypeId.Story, "B");
        var c = await CreateAsync(store, project, WorkItemTypeId.Story, "C");

        Written(await store.ReorderAsync(a.Id, 1, c.Id));

        var orderOfA = (await store.GetAsync(a.Id)).BacklogOrder;
        var orderOfB = (await store.GetAsync(b.Id)).BacklogOrder;
        var orderOfC = (await store.GetAsync(c.Id)).BacklogOrder;
        string.CompareOrdinal(orderOfA, orderOfC).ShouldBeLessThan(0);
        string.CompareOrdinal(orderOfC, orderOfB).ShouldBeLessThan(0);
    }

    [Test]
    public async Task Remove_ShouldMoveToTheRemovedStateAndKeepTheItem()
    {
        var store = CreateStore(new PassThroughWorkItemRules(), Actor);
        var project = await ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Doomed");

        Written(await store.RemoveAsync(story.Id, 1));

        var removed = await store.GetAsync(story.Id);
        removed.StateId.ShouldBe(project.StateId(WorkItemTypeId.Story, StateCategory.Removed));
        removed.Title.ShouldBe("Doomed");
        removed.Revision.ShouldBe(2);
    }

    [Test]
    public async Task Patch_WhenInTransactionHookGiven_ShouldRunItWithTheWrite()
    {
        var store = CreateStore(new PassThroughWorkItemRules(), Actor);
        var project = await ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Hooked");
        var ran = 0;

        var options = new WriteOptions((_, _) =>
        {
            ran++;
            return Task.CompletedTask;
        }, RevisionOrigin.User);
        Written(await store.PatchAsync(story.Id, new WorkItemPatch(1, new WorkItemChanges { Title = new("Done") }), options));

        ran.ShouldBe(1);
        (await store.GetAsync(story.Id)).Title.ShouldBe("Done");
    }

    [Test]
    public async Task Patch_WhenInTransactionHookFails_ShouldRollTheWriteBack()
    {
        var store = CreateStore(new PassThroughWorkItemRules(), Actor);
        var project = await ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Hooked");
        var options = new WriteOptions((_, _) => throw new InvalidOperationException("hook failed"), RevisionOrigin.User);

        await Should.ThrowAsync<InvalidOperationException>(
            () => store.PatchAsync(story.Id, new WorkItemPatch(1, new WorkItemChanges { Title = new("Never") }), options));

        var unchanged = await store.GetAsync(story.Id);
        unchanged.Title.ShouldBe("Hooked");
        unchanged.Revision.ShouldBe(1);
        (await store.GetRevisionsAsync(story.Id)).Count.ShouldBe(1);
    }

    [Test]
    public async Task Write_WhenCreatedAndPatched_ShouldCallTheRulesOncePerWriteWithTheActor()
    {
        var rules = new RecordingWorkItemRules();
        var store = CreateStore(rules, Actor);
        var project = await ArrangeProjectAsync();

        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Ruled");
        rules.Calls.Count.ShouldBe(1);
        rules.Calls[0].Before.ShouldBeNull();
        rules.Calls[0].ActorId.ShouldBe(Actor);

        Written(await store.PatchAsync(story.Id, new WorkItemPatch(1, new WorkItemChanges { Title = new("Ruled again") }), WriteOptions.User));
        rules.Calls.Count.ShouldBe(2);
        rules.Calls[1].Before.ShouldNotBeNull().Id.ShouldBe(story.Id);
        rules.Calls[1].Change.Title.ShouldBe("Ruled again");
        rules.Calls[1].ActorId.ShouldBe(Actor);
    }

    [Test]
    public async Task Patch_WhenStale_ShouldNotCallTheRules()
    {
        var rules = new RecordingWorkItemRules();
        var store = CreateStore(rules, Actor);
        var project = await ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Ruled");
        Written(await store.PatchAsync(story.Id, new WorkItemPatch(1, new WorkItemChanges { Points = new(1m) }), WriteOptions.User));
        var callsBefore = rules.Calls.Count;

        var outcome = await store.PatchAsync(story.Id, new WorkItemPatch(1, new WorkItemChanges { Points = new(2m) }), WriteOptions.User);

        outcome.Conflict.ShouldNotBeNull();
        rules.Calls.Count.ShouldBe(callsBefore);
    }

    [Test]
    public async Task Patch_WhenRulesAmendTheChange_ShouldWriteTheAmendment()
    {
        var priya = SeedIds.Users.Priya;
        var rules = new RecordingWorkItemRules(change => change.Points == 2m ? change with { AssigneeId = priya } : change);
        var store = CreateStore(rules, Actor);
        var project = await ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Amended");

        var result = Written(await store.PatchAsync(story.Id, new WorkItemPatch(1, new WorkItemChanges { Points = new(2m) }), WriteOptions.User));

        (await store.GetAsync(story.Id)).AssigneeId.ShouldBe(priya);
        result.Changes.Select(c => c.Field).ShouldBe(["assignee", "points"]);
    }

    [Test]
    public async Task Move_ShouldPassTheNewColumnOrderToTheRules()
    {
        var rules = new RecordingWorkItemRules();
        var store = CreateStore(rules, Actor);
        var project = await ArrangeProjectAsync();
        var first = await CreateAsync(store, project, WorkItemTypeId.Story, "First");
        var second = await CreateAsync(store, project, WorkItemTypeId.Story, "Second");
        Written(await store.MoveAsync(first.Id, new MoveTarget(1, project.ColumnId(0), false, null, null, null), WriteOptions.User));
        var firstOrder = (await store.GetAsync(first.Id)).ColumnOrder.ShouldNotBeNull();

        Written(await store.MoveAsync(second.Id, new MoveTarget(1, project.ColumnId(0), false, null, first.Id, null), WriteOptions.User));

        var proposed = rules.Calls[^1].Change;
        var order = proposed.ColumnOrder.ShouldNotBeNull();
        string.CompareOrdinal(order, firstOrder).ShouldBeLessThan(0);
        (await store.GetAsync(second.Id)).ColumnOrder.ShouldBe(order);
        proposed.ColumnId.ShouldBe(project.ColumnId(0));
    }

    [Test]
    public async Task Move_WhenRulesAmendBacklogOrder_ShouldWriteIt()
    {
        var rules = new RecordingWorkItemRules(change => change.ColumnId is null ? change : change with { BacklogOrder = "zzz-amended" });
        var store = CreateStore(rules, Actor);
        var project = await ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Card");

        Written(await store.MoveAsync(story.Id, new MoveTarget(1, project.ColumnId(1), false, null, null, null), WriteOptions.User));

        (await store.GetAsync(story.Id)).BacklogOrder.ShouldBe("zzz-amended");
    }

    [Test]
    public async Task Move_WhenTaskboardCell_ShouldSetStateAndCellOrderInOneRevision()
    {
        var store = CreateStore(new PassThroughWorkItemRules(), Actor);
        var project = await ArrangeProjectAsync();
        var feature = await CreateAsync(store, project, WorkItemTypeId.Feature, "Feature");
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Story", feature.Id);
        var first = await CreateAsync(store, project, WorkItemTypeId.Task, "First task", story.Id);
        var second = await CreateAsync(store, project, WorkItemTypeId.Task, "Second task", story.Id);
        var active = new WorkItemChanges { State = new(project.StateId(WorkItemTypeId.Task, StateCategory.InProgress)) };
        Written(await store.MoveAsync(first.Id, new MoveTarget(1, null, false, null, null, active), WriteOptions.User));

        var result = Written(await store.MoveAsync(second.Id, new MoveTarget(1, null, false, null, first.Id, active), WriteOptions.User));

        result.Revision.ShouldBe(2);
        result.Changes.Select(c => c.Field).ShouldBe(["state"]);
        var moved = await store.GetAsync(second.Id);
        var other = await store.GetAsync(first.Id);
        moved.StateId.ShouldBe(project.StateId(WorkItemTypeId.Task, StateCategory.InProgress));
        moved.ColumnId.ShouldBeNull();
        string.CompareOrdinal(moved.ColumnOrder.ShouldNotBeNull(), other.ColumnOrder.ShouldNotBeNull()).ShouldBeLessThan(0);
        (await store.GetRevisionsAsync(second.Id)).Count.ShouldBe(2);
    }

    [Test]
    public async Task Move_ShouldCallTheRulesOnceWithTheActor()
    {
        var rules = new RecordingWorkItemRules();
        var store = CreateStore(rules, Actor);
        var project = await ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Card");
        var callsBefore = rules.Calls.Count;

        Written(await store.MoveAsync(story.Id, new MoveTarget(1, project.ColumnId(1), false, null, null, null), WriteOptions.User));

        rules.Calls.Count.ShouldBe(callsBefore + 1);
        rules.Calls[^1].ActorId.ShouldBe(Actor);
        rules.Calls[^1].Before.ShouldNotBeNull().Id.ShouldBe(story.Id);
    }

    [Test]
    public async Task PatchMany_ShouldCallTheRulesOncePerItem()
    {
        var rules = new RecordingWorkItemRules();
        var store = CreateStore(rules, Actor);
        var project = await ArrangeProjectAsync();
        var first = await CreateAsync(store, project, WorkItemTypeId.Story, "First");
        var second = await CreateAsync(store, project, WorkItemTypeId.Story, "Second");
        var callsBefore = rules.Calls.Count;

        Written(await store.PatchAsync(first.Id, new WorkItemPatch(1, new WorkItemChanges { Points = new(1m) }), WriteOptions.User));
        var callsAfterOne = rules.Calls.Count;
        var outcome = await store.PatchManyAsync(
            [
                (first.Id, new WorkItemPatch(2, new WorkItemChanges { Points = new(3m) })),
                (second.Id, new WorkItemPatch(1, new WorkItemChanges { Points = new(2m) })),
            ],
            WriteOptions.User);

        outcome.Written.ShouldNotBeNull().Count.ShouldBe(2);
        callsAfterOne.ShouldBe(callsBefore + 1);
        rules.Calls.Count.ShouldBe(callsAfterOne + 2);
        rules.Calls.Skip(callsAfterOne).Select(c => c.ActorId).ShouldAllBe(actor => actor == Actor);
    }

    [Test]
    public async Task Patch_WhenRulesReturnNotices_ShouldReturnThem()
    {
        var rules = new RecordingWorkItemRules(notices: ["pulled"]);
        var store = CreateStore(rules, Actor);
        var project = await ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Noticed");

        var result = Written(await store.PatchAsync(story.Id, new WorkItemPatch(1, new WorkItemChanges { Points = new(1m) }), WriteOptions.User));

        result.Notices.ShouldBe(["pulled"]);
    }

    [Test]
    public async Task Reorder_ShouldWriteNoRevision()
    {
        var store = CreateStore(new PassThroughWorkItemRules(), Actor);
        var project = await ArrangeProjectAsync();
        var first = await CreateAsync(store, project, WorkItemTypeId.Story, "First");
        var second = await CreateAsync(store, project, WorkItemTypeId.Story, "Second");

        var result = Written(await store.ReorderAsync(second.Id, 1, first.Id));

        result.Revision.ShouldBe(1);
        result.Changes.ShouldBeEmpty();
        (await store.GetAsync(second.Id)).Revision.ShouldBe(1);
        (await store.GetRevisionsAsync(second.Id)).Count.ShouldBe(1);
    }

    [Test]
    public async Task Move_WhenNoFieldChanges_ShouldWriteNoRevision()
    {
        var store = CreateStore(new PassThroughWorkItemRules(), Actor);
        var project = await ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Card");

        var result = Written(await store.MoveAsync(story.Id, new MoveTarget(1, project.ColumnId(1), false, null, null, null), WriteOptions.User));

        result.Revision.ShouldBe(1);
        result.Changes.ShouldBeEmpty();
        (await store.GetAsync(story.Id)).ColumnId.ShouldBe(project.ColumnId(1));
        (await store.GetRevisionsAsync(story.Id)).Count.ShouldBe(1);
    }

    [Test]
    public async Task Patch_WhenAnEarlierLinkIsRemovedAndAddedAgain_ShouldKeepItsPlace()
    {
        var store = CreateStore(new PassThroughWorkItemRules(), Actor);
        var project = await ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Story");
        var first = new LinkValue(LinkType.Url, null, null, "https://example.test/first", "Doc");
        var second = new LinkValue(LinkType.Url, null, null, "https://example.test/second", "Doc");
        Written(await store.PatchAsync(story.Id, new WorkItemPatch(1, new WorkItemChanges { AddLinks = [first, second] }), WriteOptions.User));
        var firstId = (await store.GetAsync(story.Id)).Links[0].Id;
        Written(await store.PatchAsync(story.Id, new WorkItemPatch(2, new WorkItemChanges { RemoveLinkIds = [firstId] }), WriteOptions.User));

        Written(await store.PatchAsync(story.Id, new WorkItemPatch(3, new WorkItemChanges { AddLinks = [first] }), WriteOptions.User));

        (await store.GetAsync(story.Id)).Links.Select(l => l.Value.Url).ShouldBe(["https://example.test/first", "https://example.test/second"]);
    }

    [Test]
    public async Task Create_WhenFeatureHasGoalStatusAndSlug_ShouldStoreThemWithTheVerifiedStamp()
    {
        var store = CreateStore(new PassThroughWorkItemRules(), Actor);
        var project = await ArrangeProjectAsync();

        var created = Written(await store.CreateAsync(
            new WorkItemCreate(project.ProjectId, WorkItemTypeId.Feature, "Checkout v2", null, null, null) { Goal = "Pay in two taps.", StatusLine = "Staging works.", Slug = "checkout-v2" },
            new WriteOptions(null, RevisionOrigin.User, Via: "create_feature")));

        var feature = await store.GetAsync(created.Id);
        feature.Goal.ShouldBe("Pay in two taps.");
        feature.StatusLine.ShouldBe("Staging works.");
        feature.Slug.ShouldBe("checkout-v2");
        feature.StartedOn.ShouldBe(DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime));
        feature.StatusVerifiedAt.ShouldNotBeNull();
        feature.StatusVerifiedBy.ShouldBe(Actor);
        feature.StatusVerifiedVia.ShouldBe("create_feature");
        created.Changes.Select(c => c.Field).ShouldBeSubsetOf(FieldIds.All.Select(f => f.Id));
        created.Changes.Select(c => c.Field).Where(f => f is "goal" or "statusLine" or "statusVerifiedAt").ShouldBe(["goal", "statusLine", "statusVerifiedAt"]);
    }

    [Test]
    public async Task Create_WhenFeatureGivesStartedOn_ShouldKeepIt()
    {
        var store = CreateStore(new PassThroughWorkItemRules(), Actor);
        var project = await ArrangeProjectAsync();

        var created = Written(await store.CreateAsync(
            new WorkItemCreate(project.ProjectId, WorkItemTypeId.Feature, "Older", null, null, null) { StartedOn = new DateOnly(2026, 10, 12) }, WriteOptions.User));

        var feature = await store.GetAsync(created.Id);
        feature.StartedOn.ShouldBe(new DateOnly(2026, 10, 12));
        feature.Slug.ShouldBeNull();
        feature.StatusVerifiedAt.ShouldBeNull();
    }

    [Test]
    public async Task Create_WhenSlugTaken_ShouldBeConflictOnTheSlug()
    {
        var store = CreateStore(new PassThroughWorkItemRules(), Actor);
        var project = await ArrangeProjectAsync();
        var other = await ArrangeProjectAsync();
        await CreateFeatureAsync(store, project, "First", "taken");

        var error = await Should.ThrowAsync<ApiException>(() => CreateFeatureAsync(store, project, "Second", "taken"));

        error.Code.ShouldBe(ApiErrorCodes.SlugTaken);
        error.Field.ShouldBe("slug");
        (await CreateFeatureAsync(store, other, "Elsewhere", "taken")).Revision.ShouldBe(1);
    }

    [TestCase("Not Valid")]
    [TestCase("double--hyphen")]
    [TestCase("trailing-")]
    [TestCase("")]
    public async Task Create_WhenSlugInvalid_ShouldBeValidationOnTheSlug(string slug)
    {
        var store = CreateStore(new PassThroughWorkItemRules(), Actor);
        var project = await ArrangeProjectAsync();

        var error = await Should.ThrowAsync<ApiException>(() => CreateFeatureAsync(store, project, "Bad slug", slug));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("slug");
    }

    [Test]
    public async Task Create_WhenStoryHasSlugOrGoal_ShouldBeValidation()
    {
        var store = CreateStore(new PassThroughWorkItemRules(), Actor);
        var project = await ArrangeProjectAsync();

        var slug = await Should.ThrowAsync<ApiException>(() => store.CreateAsync(
            new WorkItemCreate(project.ProjectId, WorkItemTypeId.Story, "Story", null, null, null) { Slug = "a-story" }, WriteOptions.User));
        var goal = await Should.ThrowAsync<ApiException>(() => store.CreateAsync(
            new WorkItemCreate(project.ProjectId, WorkItemTypeId.Story, "Story", null, null, null) { Goal = "A goal" }, WriteOptions.User));

        slug.Code.ShouldBe(ApiErrorCodes.Validation);
        goal.Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Patch_WhenFeatureGoalAndStatusSet_ShouldWriteThemAndStampVerified()
    {
        var store = CreateStore(new PassThroughWorkItemRules(), Actor);
        var project = await ArrangeProjectAsync();
        var feature = await CreateAsync(store, project, WorkItemTypeId.Feature, "Feature");

        var result = Written(await store.PatchAsync(
            feature.Id,
            new WorkItemPatch(1, new WorkItemChanges { Goal = new("Goal"), StatusLine = new("Status") }),
            new WriteOptions(null, RevisionOrigin.User, Via: "set_status")));

        result.Changes.Select(c => c.Field).ShouldBe(["goal", "statusLine", "statusVerifiedAt"]);
        var stored = await store.GetAsync(feature.Id);
        stored.Goal.ShouldBe("Goal");
        stored.StatusLine.ShouldBe("Status");
        stored.StatusVerifiedAt.ShouldNotBeNull();
        stored.StatusVerifiedBy.ShouldBe(Actor);
        stored.StatusVerifiedVia.ShouldBe("set_status");
        stored.StatusVerifiedAgent.ShouldBeNull();
        (await store.GetRevisionsAsync(feature.Id))[1].Changes.Select(c => c.Field).ShouldBe(["goal", "statusLine", "statusVerifiedAt"]);
    }

    [Test]
    public async Task Patch_WhenStatusLineWrittenUnchanged_ShouldStillStampVerifiedAt()
    {
        var store = CreateStore(new PassThroughWorkItemRules(), Actor);
        var project = await ArrangeProjectAsync();
        var feature = await CreateAsync(store, project, WorkItemTypeId.Feature, "Feature");
        Written(await store.PatchAsync(feature.Id, new WorkItemPatch(1, new WorkItemChanges { StatusLine = new("Same") }), WriteOptions.User));
        var first = (await store.GetAsync(feature.Id)).StatusVerifiedAt;

        var again = Written(await store.PatchAsync(feature.Id, new WorkItemPatch(2, new WorkItemChanges { StatusLine = new("Same") }), WriteOptions.User));

        again.Changes.Select(c => c.Field).ShouldBe(["statusVerifiedAt"]);
        (await store.GetAsync(feature.Id)).StatusVerifiedAt!.Value.ShouldBeGreaterThan(first!.Value);
    }

    [Test]
    public async Task Patch_WhenConfirmStatusOnly_ShouldWriteOneRevisionWithVerifiedAtOnly()
    {
        var store = CreateStore(new PassThroughWorkItemRules(), Actor);
        var project = await ArrangeProjectAsync();
        var feature = await CreateAsync(store, project, WorkItemTypeId.Feature, "Feature");
        Written(await store.PatchAsync(feature.Id, new WorkItemPatch(1, new WorkItemChanges { StatusLine = new("Going well") }), WriteOptions.User));
        var before = await store.GetAsync(feature.Id);

        var result = Written(await store.PatchAsync(
            feature.Id, new WorkItemPatch(2, new WorkItemChanges { ConfirmStatus = true }), new WriteOptions(null, RevisionOrigin.User, Via: "confirm_status")));

        result.Revision.ShouldBe(3);
        result.Changes.Select(c => c.Field).ShouldBe(["statusVerifiedAt"]);
        var after = await store.GetAsync(feature.Id);
        after.StatusLine.ShouldBe("Going well");
        after.StatusVerifiedAt!.Value.ShouldBeGreaterThan(before.StatusVerifiedAt!.Value);
        after.StatusVerifiedVia.ShouldBe("confirm_status");
        (await store.GetRevisionsAsync(feature.Id)).Count.ShouldBe(3);
    }

    [Test]
    public async Task Patch_WhenOnlyTheTitleChanges_ShouldKeepTheVerifiedStamp()
    {
        var store = CreateStore(new PassThroughWorkItemRules(), Actor);
        var project = await ArrangeProjectAsync();
        var feature = await CreateAsync(store, project, WorkItemTypeId.Feature, "Feature");
        Written(await store.PatchAsync(feature.Id, new WorkItemPatch(1, new WorkItemChanges { StatusLine = new("Going well") }), new WriteOptions(null, RevisionOrigin.User, Via: "set_status")));
        var before = await store.GetAsync(feature.Id);

        Written(await store.PatchAsync(feature.Id, new WorkItemPatch(2, new WorkItemChanges { Title = new("Renamed") }), WriteOptions.User));

        var after = await store.GetAsync(feature.Id);
        after.StatusVerifiedAt.ShouldBe(before.StatusVerifiedAt);
        after.StatusVerifiedBy.ShouldBe(before.StatusVerifiedBy);
        after.StatusVerifiedVia.ShouldBe("set_status");
    }

    [Test]
    public async Task Patch_WhenStoryGetsGoal_ShouldBeValidation()
    {
        var store = CreateStore(new PassThroughWorkItemRules(), Actor);
        var project = await ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Story");

        var goal = await Should.ThrowAsync<ApiException>(() => store.PatchAsync(story.Id, new WorkItemPatch(1, new WorkItemChanges { Goal = new("Goal") }), WriteOptions.User));
        var status = await Should.ThrowAsync<ApiException>(() => store.PatchAsync(story.Id, new WorkItemPatch(1, new WorkItemChanges { StatusLine = new("Status") }), WriteOptions.User));
        var confirm = await Should.ThrowAsync<ApiException>(() => store.PatchAsync(story.Id, new WorkItemPatch(1, new WorkItemChanges { ConfirmStatus = true }), WriteOptions.User));

        goal.Code.ShouldBe(ApiErrorCodes.Validation);
        goal.Field.ShouldBe("goal");
        status.Code.ShouldBe(ApiErrorCodes.Validation);
        confirm.Code.ShouldBe(ApiErrorCodes.Validation);
        (await store.GetAsync(story.Id)).Revision.ShouldBe(1);
    }

    [TestCase(1001, false)]
    [TestCase(1000, true)]
    public async Task Patch_WhenGoalLength_ShouldBeCheckedAgainst1000(int length, bool accepted)
    {
        var store = CreateStore(new PassThroughWorkItemRules(), Actor);
        var project = await ArrangeProjectAsync();
        var feature = await CreateAsync(store, project, WorkItemTypeId.Feature, "Feature");
        var patch = new WorkItemPatch(1, new WorkItemChanges { Goal = new(new string('g', length)) });

        if (accepted)
        {
            Written(await store.PatchAsync(feature.Id, patch, WriteOptions.User));
            return;
        }

        (await Should.ThrowAsync<ApiException>(() => store.PatchAsync(feature.Id, patch, WriteOptions.User))).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [TestCase(501, null, false)]
    [TestCase(500, null, true)]
    [TestCase(5, "\n", false)]
    [TestCase(5, "\r", false)]
    public async Task Patch_WhenStatusLine_ShouldBeOneLineOfAtMost500(int length, string? lineBreak, bool accepted)
    {
        var store = CreateStore(new PassThroughWorkItemRules(), Actor);
        var project = await ArrangeProjectAsync();
        var feature = await CreateAsync(store, project, WorkItemTypeId.Feature, "Feature");
        var text = lineBreak is null ? new string('s', length) : new string('s', length) + lineBreak + "end";
        var patch = new WorkItemPatch(1, new WorkItemChanges { StatusLine = new(text) });

        if (accepted)
        {
            Written(await store.PatchAsync(feature.Id, patch, WriteOptions.User));
            return;
        }

        (await Should.ThrowAsync<ApiException>(() => store.PatchAsync(feature.Id, patch, WriteOptions.User))).Field.ShouldBe("statusLine");
    }

    private static async Task<WriteResult> CreateFeatureAsync(IWorkItemStore store, ArrangedProject project, string title, string slug)
        => Written(await store.CreateAsync(new WorkItemCreate(project.ProjectId, WorkItemTypeId.Feature, title, null, null, null) { Slug = slug }, WriteOptions.User));
}
