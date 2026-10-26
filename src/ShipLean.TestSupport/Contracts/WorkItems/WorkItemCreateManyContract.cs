using ShipLean.Application.Fakes.WorkItems;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using Shouldly;
using static ShipLean.TestSupport.Contracts.WorkItems.WorkItemContractSupport;

namespace ShipLean.TestSupport.Contracts.WorkItems;

// Behaviour of IWorkItemStore.CreateManyAsync, whatever the store keeps data in. Same arrange hooks as WorkItemStoreContract.
[TestFixture]
public abstract class WorkItemCreateManyContract
{
    protected abstract IWorkItemStore CreateStore(IWorkItemRules rules, Guid actorId);

    protected abstract Task<ArrangedProject> ArrangeProjectAsync();

    [Test]
    public async Task CreateMany_ShouldAllocateKeysInOrder()
    {
        var store = CreateStore(new PassThroughWorkItemRules(), Actor);
        var project = await ArrangeProjectAsync();
        var before = await CreateAsync(store, project, WorkItemTypeId.Story, "Existing");

        var outcomes = await store.CreateManyAsync(
            [Create(project, WorkItemTypeId.Story, "A"), Create(project, WorkItemTypeId.Feature, "B"), Create(project, WorkItemTypeId.Story, "C")],
            WriteOptions.User);

        var written = outcomes.Select(Written).ToList();
        var firstNumber = (await store.GetAsync(before.Id)).Number + 1;
        written.Select(w => w.Key).ShouldBe([$"{project.Key}-{firstNumber}", $"{project.Key}-{firstNumber + 1}", $"{project.Key}-{firstNumber + 2}"]);
        (await store.GetAsync(written[1].Id)).Title.ShouldBe("B");
        (await store.GetAsync(written[2].Id)).Revision.ShouldBe(1);
    }

    [Test]
    public async Task CreateMany_WhenOneInvalid_ShouldCreateNone()
    {
        var store = CreateStore(new PassThroughWorkItemRules(), Actor);
        var project = await ArrangeProjectAsync();
        var epic = await CreateAsync(store, project, WorkItemTypeId.Epic, "Epic");

        var error = await Should.ThrowAsync<ApiException>(() => store.CreateManyAsync(
            [Create(project, WorkItemTypeId.Story, "Fine"), Create(project, WorkItemTypeId.Story, "A story cannot sit under an epic", epic.Id)],
            WriteOptions.User));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
        var next = await CreateAsync(store, project, WorkItemTypeId.Story, "After");
        // "Fine" would have taken the number right after the epic; the store has no list read, so the proof that it was not
        // written is that the next create got exactly that number and the item there is "After".
        var slot = await store.GetAsync(next.Id);
        slot.Title.ShouldBe("After");
        slot.Number.ShouldBe((await store.GetAsync(epic.Id)).Number + 1);
        next.Key.ShouldBe($"{project.Key}-{slot.Number}");
    }

    [Test]
    public async Task CreateMany_WhenEmpty_ShouldReturnNoOutcomes()
    {
        var store = CreateStore(new PassThroughWorkItemRules(), Actor);
        await ArrangeProjectAsync();

        (await store.CreateManyAsync([], WriteOptions.User)).ShouldBeEmpty();
    }

    [Test]
    public async Task CreateMany_WhenItemsInTwoProjects_ShouldBeValidation()
    {
        var store = CreateStore(new PassThroughWorkItemRules(), Actor);
        var first = await ArrangeProjectAsync();
        var second = await ArrangeProjectAsync();
        var before = await CreateAsync(store, first, WorkItemTypeId.Story, "Before");

        var error = await Should.ThrowAsync<ApiException>(() => store.CreateManyAsync(
            [Create(first, WorkItemTypeId.Story, "One"), Create(second, WorkItemTypeId.Story, "Two")], WriteOptions.User));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
        // Nothing was written: the next create takes the number right after the first.
        var after = await CreateAsync(store, first, WorkItemTypeId.Story, "After");
        (await store.GetAsync(after.Id)).Number.ShouldBe((await store.GetAsync(before.Id)).Number + 1);
    }

    // IWorkItemStore is frozen, so the order is a contract of CreateManyAsync itself: the batch is on top of the backlog in input order.
    [Test]
    public async Task CreateMany_ShouldKeepInputOrderAtTopOfBacklog()
    {
        var store = CreateStore(new PassThroughWorkItemRules(), Actor);
        var project = await ArrangeProjectAsync();
        var existing = await CreateAsync(store, project, WorkItemTypeId.Story, "Existing");

        var outcomes = await store.CreateManyAsync(
            [Create(project, WorkItemTypeId.Story, "A"), Create(project, WorkItemTypeId.Story, "B"), Create(project, WorkItemTypeId.Story, "C")],
            WriteOptions.User);

        var orders = new List<string>();
        foreach (var id in outcomes.Select(Written).Select(w => w.Id).Append(existing.Id))
        {
            orders.Add((await store.GetAsync(id)).BacklogOrder);
        }

        orders.OrderBy(o => o, StringComparer.Ordinal).ShouldBe(orders);
        orders.Distinct().Count().ShouldBe(4);
    }

    [Test]
    public async Task CreateMany_WhenBacklogEmpty_ShouldKeepInputOrder()
    {
        var store = CreateStore(new PassThroughWorkItemRules(), Actor);
        var project = await ArrangeProjectAsync();

        var outcomes = await store.CreateManyAsync(
            [Create(project, WorkItemTypeId.Story, "A"), Create(project, WorkItemTypeId.Story, "B"), Create(project, WorkItemTypeId.Story, "C")],
            WriteOptions.User);

        var orders = new List<string>();
        foreach (var id in outcomes.Select(Written).Select(w => w.Id))
        {
            orders.Add((await store.GetAsync(id)).BacklogOrder);
        }

        orders.OrderBy(o => o, StringComparer.Ordinal).ShouldBe(orders);
        orders.Distinct().Count().ShouldBe(3);
    }

    private static WorkItemCreate Create(ArrangedProject project, WorkItemTypeId type, string title, Guid? parentId = null)
        => new(project.ProjectId, type, title, parentId, null, null);
}
