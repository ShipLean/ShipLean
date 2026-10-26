using System.Data.Common;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Sprints;
using Shouldly;

namespace ShipLean.TestSupport.Contracts.Sprints;

// Behaviour every ISprintStore must have, whatever it stores data in.
[TestFixture]
public abstract class SprintStoreContract
{
    protected abstract ISprintStore CreateStore();

    protected abstract Task<Guid> ArrangeProjectAsync();

    protected abstract Task<Guid> ArrangeMemberAsync(Guid projectId);

    protected abstract Task<DbTransaction> BeginTransactionAsync();

    private static DateOnly Today => DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime);

    private static SprintCreate Sprint(string name, int startOffset, int length = 13) => new(name, Today.AddDays(startOffset), Today.AddDays(startOffset + length), "goal");

    [Test]
    public async Task Create_ShouldCarryStatusAndLabels()
    {
        var store = CreateStore();
        var project = await ArrangeProjectAsync();

        var future = await store.CreateAsync(project, Sprint("Soon", 5), await BeginTransactionAsync());
        var current = await store.CreateAsync(project, Sprint("Now", -20), null);

        future.Status.ShouldBe(SprintStatus.Future);
        future.TimeLabel.ShouldBe("Starts in 5 days");
        current.Status.ShouldBe(SprintStatus.Past);
        current.DatesLabel.ShouldContain("–");
        future.Goal.ShouldBe("goal");
    }

    [Test]
    public async Task Sprints_WhenOverlapping_ShouldBeValidation()
    {
        var store = CreateStore();
        var project = await ArrangeProjectAsync();
        await store.CreateAsync(project, Sprint("A", 0), null);
        var later = await store.CreateAsync(project, Sprint("B", 14), null);

        var onCreate = await Should.ThrowAsync<ApiException>(() => store.CreateAsync(project, Sprint("C", 10), null));
        var onUpdate = await Should.ThrowAsync<ApiException>(() => store.UpdateAsync(later.Id, new SprintUpdate(null, Today.AddDays(5), null, null)));

        onCreate.Code.ShouldBe(ApiErrorCodes.Validation);
        onUpdate.Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Create_WhenEndBeforeStartOrNameTaken_ShouldRefuse()
    {
        var store = CreateStore();
        var project = await ArrangeProjectAsync();
        await store.CreateAsync(project, Sprint("A", 0), null);

        var backwards = await Should.ThrowAsync<ApiException>(() => store.CreateAsync(project, new SprintCreate("B", Today.AddDays(30), Today.AddDays(20), ""), null));
        var duplicate = await Should.ThrowAsync<ApiException>(() => store.CreateAsync(project, Sprint("A", 30), null));

        backwards.Code.ShouldBe(ApiErrorCodes.Validation);
        duplicate.Code.ShouldBe(ApiErrorCodes.Conflict);
    }

    [Test]
    public async Task Update_ShouldChangeOnlyTheGivenFields()
    {
        var store = CreateStore();
        var project = await ArrangeProjectAsync();
        var created = await store.CreateAsync(project, Sprint("A", 0), null);

        var updated = await store.UpdateAsync(created.Id, new SprintUpdate(null, null, null, "new goal"));

        (updated.Name, updated.StartDate, updated.EndDate, updated.Goal).ShouldBe((created.Name, created.StartDate, created.EndDate, "new goal"));
    }

    [Test]
    public async Task Capacity_WhenPut_ShouldSumTeam()
    {
        var store = CreateStore();
        var project = await ArrangeProjectAsync();
        var sprint = await store.CreateAsync(project, Sprint("A", 0), null);
        var first = await ArrangeMemberAsync(project);
        var second = await ArrangeMemberAsync(project);

        var capacity = await store.PutCapacityAsync(sprint.Id, new SprintCapacityPut([new MemberCapacityPut(first, 6, null), new MemberCapacityPut(second, 4.5m, "away Fri")]));

        capacity.TeamTotal.ShouldBe(10.5m);
        capacity.Members.Single(m => m.User.Id == second).Note.ShouldBe("away Fri");
        (await store.GetCapacityAsync(sprint.Id)).TeamTotal.ShouldBe(10.5m);
    }

    [Test]
    public async Task Capacity_WhenMemberLeftOut_ShouldClearTheirEntry()
    {
        var store = CreateStore();
        var project = await ArrangeProjectAsync();
        var sprint = await store.CreateAsync(project, Sprint("A", 0), null);
        var first = await ArrangeMemberAsync(project);
        var second = await ArrangeMemberAsync(project);
        await store.PutCapacityAsync(sprint.Id, new SprintCapacityPut([new MemberCapacityPut(first, 6, null), new MemberCapacityPut(second, 4, null)]));

        var replaced = await store.PutCapacityAsync(sprint.Id, new SprintCapacityPut([new MemberCapacityPut(first, 5, "half day")]));

        replaced.Members.Select(m => m.User.Id).ShouldBe([first]);
        replaced.TeamTotal.ShouldBe(5m);
        var again = await store.PutCapacityAsync(sprint.Id, new SprintCapacityPut([new MemberCapacityPut(first, 5, null), new MemberCapacityPut(second, 3, null)]));
        again.Members.Single(m => m.User.Id == second).Points.ShouldBe(3m);
        again.TeamTotal.ShouldBe(8m);
    }

    [Test]
    public async Task Capacity_WhenNoEntries_ShouldListNoMembers()
    {
        var store = CreateStore();
        var project = await ArrangeProjectAsync();
        var sprint = await store.CreateAsync(project, Sprint("A", 0), null);
        await ArrangeMemberAsync(project);

        var untouched = await store.GetCapacityAsync(sprint.Id);
        var cleared = await store.PutCapacityAsync(sprint.Id, new SprintCapacityPut([]));

        untouched.Members.ShouldBeEmpty();
        untouched.TeamTotal.ShouldBe(0m);
        cleared.Members.ShouldBeEmpty();
    }

    [Test]
    public async Task Capacity_WhenNegativeOrNotAMember_ShouldBeValidation()
    {
        var store = CreateStore();
        var project = await ArrangeProjectAsync();
        var sprint = await store.CreateAsync(project, Sprint("A", 0), null);
        var member = await ArrangeMemberAsync(project);
        var stranger = await ArrangeMemberAsync(await ArrangeProjectAsync());

        var negative = await Should.ThrowAsync<ApiException>(() => store.PutCapacityAsync(sprint.Id, new SprintCapacityPut([new MemberCapacityPut(member, -1, null)])));
        var outsider = await Should.ThrowAsync<ApiException>(() => store.PutCapacityAsync(sprint.Id, new SprintCapacityPut([new MemberCapacityPut(stranger, 3, null)])));

        negative.Code.ShouldBe(ApiErrorCodes.Validation);
        outsider.Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Rollover_WhenSprintNotEnded_ShouldBeValidation()
    {
        var store = CreateStore();
        var project = await ArrangeProjectAsync();
        var sprint = await store.CreateAsync(project, Sprint("A", -2), null);

        var error = await Should.ThrowAsync<ApiException>(() => store.RolloverAsync(sprint.Id, new RolloverRequest([])));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Rollover_WhenSprintEnded_ShouldMarkItDecided()
    {
        var store = CreateStore();
        var project = await ArrangeProjectAsync();
        var ended = await store.CreateAsync(project, Sprint("Old", -40), null);
        ended.RolloverStatus.ShouldBeNull();

        var decided = await store.RolloverAsync(ended.Id, new RolloverRequest([]));

        decided.RolloverStatus.ShouldBe(RolloverStatus.Decided);
        (await store.GetRolloverAsync(ended.Id)).Status.ShouldBe(RolloverStatus.Decided);
    }

    [Test]
    public async Task Rollover_WhenEndDateIsToday_ShouldRollOver()
    {
        var store = CreateStore();
        var project = await ArrangeProjectAsync();
        var endingToday = await store.CreateAsync(project, Sprint("Ends today", -13), null);

        var decided = await store.RolloverAsync(endingToday.Id, new RolloverRequest([]));

        decided.RolloverStatus.ShouldBe(RolloverStatus.Decided);
    }

    [Test]
    public async Task Update_WhenUnknownSprint_ShouldBeNotFound()
    {
        var error = await Should.ThrowAsync<ApiException>(() => CreateStore().UpdateAsync(Guid.NewGuid(), new SprintUpdate("x", null, null, null)));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }
}
