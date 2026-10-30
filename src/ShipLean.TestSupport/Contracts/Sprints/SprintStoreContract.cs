using System.Data.Common;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Holidays;
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

    // The recompute over the same data (and the same holidays) as CreateStore.
    protected abstract ISprintCapacityRecompute CreateRecompute();

    // Sets the project's holiday country and the country's holidays (the store reads them through IPublicHolidays).
    protected abstract Task ArrangeHolidaysAsync(Guid projectId, string country, params (DateOnly Date, string Name)[] holidays);

    protected abstract Task ArrangeWorkingDaysAsync(Guid projectId, params int[] isoDays);

    // Writes the sprint's capacity as an earlier save left it, bypassing the rules (velocity null: an old points-only sprint).
    protected abstract Task ArrangeStoredCapacityAsync(Guid sprintId, decimal? velocity, params (Guid UserId, decimal? Days, decimal Points)[] rows);

    // Ten working days (Monday 2099-01-05 to Sunday 2099-01-18), far in the future so it is never ended.
    protected static SprintCreate TwoWeeks(string name, int weeksLater = 0)
        => new(name, new DateOnly(2099, 1, 5).AddDays(7 * weeksLater), new DateOnly(2099, 1, 18).AddDays(7 * weeksLater), "goal");

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
    public async Task Update_WhenSprintEnded_ShouldRefuseADateChange()
    {
        var store = CreateStore();
        var project = await ArrangeProjectAsync();
        var ended = await store.CreateAsync(project, Sprint("Old", -40), null);

        var start = await Should.ThrowAsync<ApiException>(() => store.UpdateAsync(ended.Id, new SprintUpdate(null, ended.StartDate.AddDays(1), null, null)));
        var end = await Should.ThrowAsync<ApiException>(() => store.UpdateAsync(ended.Id, new SprintUpdate(null, null, ended.EndDate.AddDays(1), null)));

        start.Code.ShouldBe(ApiErrorCodes.Validation);
        start.Message.ShouldBe("An ended sprint's dates can't change.");
        end.Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Update_WhenNewEndIsBeforeToday_ShouldRefuseButAllowEndingToday()
    {
        var store = CreateStore();
        var project = await ArrangeProjectAsync();
        var running = await store.CreateAsync(project, Sprint("Running", -5), null);

        var past = await Should.ThrowAsync<ApiException>(() => store.UpdateAsync(running.Id, new SprintUpdate(null, null, Today.AddDays(-1), null)));
        var today = await store.UpdateAsync(running.Id, new SprintUpdate(null, null, Today, null));

        past.Code.ShouldBe(ApiErrorCodes.Validation);
        past.Message.ShouldBe("A sprint can't be moved to end in the past.");
        today.EndDate.ShouldBe(Today);
    }

    [Test]
    public async Task Update_WhenSprintEnded_ShouldStillRenameAndKeepSentDatesUnchanged()
    {
        var store = CreateStore();
        var project = await ArrangeProjectAsync();
        var ended = await store.CreateAsync(project, Sprint("Old", -40), null);

        var renamed = await store.UpdateAsync(ended.Id, new SprintUpdate("Old, renamed", null, null, "done"));
        var sameDates = await store.UpdateAsync(ended.Id, new SprintUpdate("Old, again", ended.StartDate, ended.EndDate, null));

        renamed.Name.ShouldBe("Old, renamed");
        renamed.Goal.ShouldBe("done");
        sameDates.Name.ShouldBe("Old, again");
        (sameDates.StartDate, sameDates.EndDate).ShouldBe((ended.StartDate, ended.EndDate));
    }

    [Test]
    public async Task Update_WhenSprintIsCurrent_ShouldMoveItsEnd()
    {
        var store = CreateStore();
        var project = await ArrangeProjectAsync();
        var current = await store.CreateAsync(project, Sprint("Now", -3), null);

        var moved = await store.UpdateAsync(current.Id, new SprintUpdate(null, null, current.EndDate.AddDays(3), null));

        moved.EndDate.ShouldBe(current.EndDate.AddDays(3));
    }

    [Test]
    public async Task Capacity_WhenPut_ShouldComputeEachMembersPointsFromVelocityAndDays()
    {
        var store = CreateStore();
        var project = await ArrangeProjectAsync();
        var sprint = await store.CreateAsync(project, TwoWeeks("A"), null);
        var first = await ArrangeMemberAsync(project);
        var second = await ArrangeMemberAsync(project);

        var capacity = await store.PutCapacityAsync(sprint.Id, new SprintCapacityPut([new MemberCapacityPut(first, null, null), new MemberCapacityPut(second, 5, "away Fri")], 30));

        capacity.Velocity.ShouldBe(30m);
        capacity.WorkingDays.ShouldBe(10);
        capacity.DefaultDays.ShouldBe(10);
        capacity.Members.Single(m => m.User.Id == first).Days.ShouldBeNull();
        capacity.Members.Single(m => m.User.Id == first).Points.ShouldBe(15m);
        capacity.Members.Single(m => m.User.Id == second).Days.ShouldBe(5m);
        capacity.Members.Single(m => m.User.Id == second).Points.ShouldBe(7.5m);
        capacity.Members.Single(m => m.User.Id == second).Note.ShouldBe("away Fri");
        capacity.TeamTotal.ShouldBe(22.5m);
        capacity.Ended.ShouldBeFalse();
        (await store.GetCapacityAsync(sprint.Id)).TeamTotal.ShouldBe(22.5m);
    }

    [Test]
    public async Task Capacity_WhenMemberLeftOut_ShouldClearTheirEntryAndShareAmongTheRest()
    {
        var store = CreateStore();
        var project = await ArrangeProjectAsync();
        var sprint = await store.CreateAsync(project, TwoWeeks("A"), null);
        var first = await ArrangeMemberAsync(project);
        var second = await ArrangeMemberAsync(project);
        await store.PutCapacityAsync(sprint.Id, new SprintCapacityPut([new MemberCapacityPut(first, null, null), new MemberCapacityPut(second, null, null)], 40));

        var replaced = await store.PutCapacityAsync(sprint.Id, new SprintCapacityPut([new MemberCapacityPut(first, 5, "half day")], 40));

        replaced.Members.Select(m => m.User.Id).ShouldBe([first]);
        replaced.TeamTotal.ShouldBe(20m);
        var again = await store.PutCapacityAsync(sprint.Id, new SprintCapacityPut([new MemberCapacityPut(first, 5, null), new MemberCapacityPut(second, 10, null)], 40));
        again.Members.Single(m => m.User.Id == second).Points.ShouldBe(20m);
        again.TeamTotal.ShouldBe(30m);
    }

    [Test]
    public async Task Capacity_WhenNoEntries_ShouldListNoMembers()
    {
        var store = CreateStore();
        var project = await ArrangeProjectAsync();
        var sprint = await store.CreateAsync(project, TwoWeeks("A"), null);
        await ArrangeMemberAsync(project);

        var untouched = await store.GetCapacityAsync(sprint.Id);
        var cleared = await store.PutCapacityAsync(sprint.Id, new SprintCapacityPut([]));

        untouched.Members.ShouldBeEmpty();
        untouched.TeamTotal.ShouldBe(0m);
        untouched.Velocity.ShouldBeNull();
        cleared.Members.ShouldBeEmpty();
    }

    [Test]
    public async Task Capacity_WhenNegativeOrNotAMemberOrOffStep_ShouldBeValidation()
    {
        var store = CreateStore();
        var project = await ArrangeProjectAsync();
        var sprint = await store.CreateAsync(project, TwoWeeks("A"), null);
        var member = await ArrangeMemberAsync(project);
        var stranger = await ArrangeMemberAsync(await ArrangeProjectAsync());

        var negative = await Should.ThrowAsync<ApiException>(() => store.PutCapacityAsync(sprint.Id, new SprintCapacityPut([new MemberCapacityPut(member, -1, null)], 30)));
        var tooMany = await Should.ThrowAsync<ApiException>(() => store.PutCapacityAsync(sprint.Id, new SprintCapacityPut([new MemberCapacityPut(member, 10.5m, null)], 30)));
        var offStep = await Should.ThrowAsync<ApiException>(() => store.PutCapacityAsync(sprint.Id, new SprintCapacityPut([new MemberCapacityPut(member, 2.25m, null)], 30)));
        var outsider = await Should.ThrowAsync<ApiException>(() => store.PutCapacityAsync(sprint.Id, new SprintCapacityPut([new MemberCapacityPut(stranger, 3, null)], 30)));

        negative.Code.ShouldBe(ApiErrorCodes.Validation);
        negative.Message.ShouldBe("Days are between 0 and 10, in steps of half a day.");
        tooMany.Code.ShouldBe(ApiErrorCodes.Validation);
        offStep.Code.ShouldBe(ApiErrorCodes.Validation);
        outsider.Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Capacity_WhenMembersHaveNoVelocityOrABadOne_ShouldBeValidation()
    {
        var store = CreateStore();
        var project = await ArrangeProjectAsync();
        var sprint = await store.CreateAsync(project, TwoWeeks("A"), null);
        var member = await ArrangeMemberAsync(project);
        var row = new MemberCapacityPut(member, null, null);

        var missing = await Should.ThrowAsync<ApiException>(() => store.PutCapacityAsync(sprint.Id, new SprintCapacityPut([row])));
        var negative = await Should.ThrowAsync<ApiException>(() => store.PutCapacityAsync(sprint.Id, new SprintCapacityPut([row], -1)));
        var huge = await Should.ThrowAsync<ApiException>(() => store.PutCapacityAsync(sprint.Id, new SprintCapacityPut([row], 10000)));
        var fine = await Should.ThrowAsync<ApiException>(() => store.PutCapacityAsync(sprint.Id, new SprintCapacityPut([row], 4.555m)));

        missing.Message.ShouldBe("Capacity needs a velocity.");
        negative.Message.ShouldBe("Velocity is zero or more points, up to 9999.99.");
        huge.Code.ShouldBe(ApiErrorCodes.Validation);
        fine.Message.ShouldBe("Velocity has at most two decimal places.");
    }

    [Test]
    public async Task Capacity_WhenSprintEnded_ShouldRefuseAnyPut()
    {
        var store = CreateStore();
        var project = await ArrangeProjectAsync();
        var ended = await store.CreateAsync(project, Sprint("Old", -40), null);
        var member = await ArrangeMemberAsync(project);

        var put = await Should.ThrowAsync<ApiException>(() => store.PutCapacityAsync(ended.Id, new SprintCapacityPut([new MemberCapacityPut(member, null, null)], 20)));
        var clear = await Should.ThrowAsync<ApiException>(() => store.PutCapacityAsync(ended.Id, new SprintCapacityPut([])));

        put.Code.ShouldBe(ApiErrorCodes.Validation);
        put.Message.ShouldBe("An ended sprint's capacity can't change.");
        clear.Message.ShouldBe("An ended sprint's capacity can't change.");
        (await store.GetCapacityAsync(ended.Id)).Ended.ShouldBeTrue();
    }

    [Test]
    public async Task Capacity_WhenOneHolidayInTenDays_ShouldLowerTheDefaultToNinetyPercent()
    {
        var store = CreateStore();
        var project = await ArrangeProjectAsync();
        await ArrangeHolidaysAsync(project, "NZ", (new DateOnly(2099, 1, 7), "Midweek day"));
        var sprint = await store.CreateAsync(project, TwoWeeks("A"), null);
        var first = await ArrangeMemberAsync(project);
        var second = await ArrangeMemberAsync(project);

        var capacity = await store.PutCapacityAsync(sprint.Id, new SprintCapacityPut([new MemberCapacityPut(first, null, null), new MemberCapacityPut(second, 10, null)], 40));

        capacity.WorkingDays.ShouldBe(10);
        capacity.DefaultDays.ShouldBe(9);
        capacity.HolidayCountry.ShouldBe("NZ");
        capacity.Holidays.ShouldBe([new CapacityHolidayDto(new DateOnly(2099, 1, 7), "Midweek day", "Wed 7 Jan")]);
        capacity.Members.Single(m => m.User.Id == first).Points.ShouldBe(18m);
        capacity.Members.Single(m => m.User.Id == second).Points.ShouldBe(20m);
    }

    [Test]
    public async Task Capacity_WhenHolidayFallsOnAWeekend_ShouldNotCountIt()
    {
        var store = CreateStore();
        var project = await ArrangeProjectAsync();
        await ArrangeHolidaysAsync(project, "NZ", (new DateOnly(2099, 1, 10), "Sunday"), (new DateOnly(2099, 1, 17), "Saturday"));
        var sprint = await store.CreateAsync(project, TwoWeeks("A"), null);

        var capacity = await store.GetCapacityAsync(sprint.Id);

        capacity.Holidays.ShouldBeEmpty();
        capacity.DefaultDays.ShouldBe(10);
    }

    [Test]
    public async Task Capacity_WhenWorkingDaysAreFewer_ShouldChangeWAndTheDays()
    {
        var store = CreateStore();
        var project = await ArrangeProjectAsync();
        await ArrangeWorkingDaysAsync(project, 1, 2, 3);
        var sprint = await store.CreateAsync(project, TwoWeeks("A"), null);
        var member = await ArrangeMemberAsync(project);

        var capacity = await store.PutCapacityAsync(sprint.Id, new SprintCapacityPut([new MemberCapacityPut(member, 6, null)], 12));

        capacity.WorkingDays.ShouldBe(6);
        capacity.Members.Single().Points.ShouldBe(12m);
    }

    [Test]
    public async Task Capacity_WhenGotWithNothingStored_ShouldDefaultVelocityFromPreviousThenOverrideThenAverage()
    {
        var store = CreateStore();
        var project = await ArrangeProjectAsync();
        var member = await ArrangeMemberAsync(project);
        var first = await store.CreateAsync(project, TwoWeeks("A"), null);
        var second = await store.CreateAsync(project, TwoWeeks("B", 2), null);

        var beforeAny = await store.GetCapacityAsync(first.Id);
        await store.PutCapacityAsync(first.Id, new SprintCapacityPut([new MemberCapacityPut(member, null, null)], 23.45m));
        var afterPrevious = await store.GetCapacityAsync(second.Id);

        beforeAny.VelocityDefault.ShouldBe(0m);
        afterPrevious.VelocityDefault.ShouldBe(23.5m);
        (await store.GetCapacityAsync(first.Id)).VelocityDefault.ShouldBe(0m);
    }

    [Test]
    public async Task Recompute_WhenHolidayAppears_ShouldLowerDefaultDaysPointsOnlyForSprintsThatCanChange()
    {
        var store = CreateStore();
        var recompute = CreateRecompute();
        var project = await ArrangeProjectAsync();
        var first = await ArrangeMemberAsync(project);
        var second = await ArrangeMemberAsync(project);
        var open = await store.CreateAsync(project, TwoWeeks("Open"), null);
        var oldStyle = await store.CreateAsync(project, TwoWeeks("Old style", 2), null);
        var ended = await store.CreateAsync(project, Sprint("Ended", -40), null);
        await store.PutCapacityAsync(open.Id, new SprintCapacityPut([new MemberCapacityPut(first, null, null), new MemberCapacityPut(second, 4, null)], 40));
        await ArrangeStoredCapacityAsync(oldStyle.Id, null, (first, null, 7m));
        await ArrangeStoredCapacityAsync(ended.Id, 40m, (first, null, 20m));
        await ArrangeHolidaysAsync(project, "NZ", (new DateOnly(2099, 1, 7), "Midweek day"));

        var changed = await recompute.RecomputeAsync(project, null);

        changed.ShouldBe([open.Id]);
        var reread = await store.GetCapacityAsync(open.Id);
        reread.Members.Single(m => m.User.Id == first).Points.ShouldBe(18m);
        reread.Members.Single(m => m.User.Id == second).Points.ShouldBe(8m);
        (await store.GetCapacityAsync(oldStyle.Id)).Members.Single().Points.ShouldBe(7m);
        (await store.GetCapacityAsync(ended.Id)).Members.Single().Points.ShouldBe(20m);
        (await recompute.RecomputeAsync(project, null)).ShouldBeEmpty();
    }

    [Test]
    public async Task Recompute_WhenTypedDaysAreAboveTheNewW_ShouldClampThemToW()
    {
        var store = CreateStore();
        var recompute = CreateRecompute();
        var project = await ArrangeProjectAsync();
        var member = await ArrangeMemberAsync(project);
        var sprint = await store.CreateAsync(project, TwoWeeks("A"), null);
        await store.PutCapacityAsync(sprint.Id, new SprintCapacityPut([new MemberCapacityPut(member, 9.5m, null)], 20));
        await ArrangeWorkingDaysAsync(project, 1, 2, 3, 4);

        var changed = await recompute.RecomputeAsync(project, null);

        changed.ShouldBe([sprint.Id]);
        var reread = await store.GetCapacityAsync(sprint.Id);
        reread.WorkingDays.ShouldBe(8);
        reread.Members.Single().Days.ShouldBe(8m);
        reread.Members.Single().Points.ShouldBe(20m);
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
