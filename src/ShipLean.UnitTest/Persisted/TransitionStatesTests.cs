using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;

namespace ShipLean.UnitTest.Persisted;

[TestFixture]
public class TransitionStatesTests
{
    private static readonly TransitionState New = new(Guid.NewGuid(), WorkItemTypeId.Story, "New", false);
    private static readonly TransitionState Active = new(Guid.NewGuid(), WorkItemTypeId.Story, "Active", false);

    private static TransitionJson Transition(TransitionState? from, TransitionState to, string? toName = null, WorkItemTypeId type = WorkItemTypeId.Story)
        => new("t1", type, from is null ? null : new StateRefJson(from.Id, from.Name), new StateRefJson(to.Id, toName ?? to.Name), [new ActionJson("a1", "agent", "Go.", null)]);

    [Test]
    public void Normalize_WhenIdIsLive_ShouldRefreshTheName()
    {
        var result = TransitionStates.Normalize([Transition(New, Active, toName: "Old name")], [New, Active]);

        result.Single().To.Name.ShouldBe("Active");
        TransitionStates.IsValid(result.Single(), [New, Active]).ShouldBeTrue();
    }

    [Test]
    public void Normalize_WhenIdIsArchivedAndNothingMatches_ShouldLeaveItInvalid()
    {
        var archived = Active with { Archived = true };
        var transition = Transition(New, Active);

        var result = TransitionStates.Normalize([transition], [New, archived]);

        result.Single().ShouldBe(transition);
        TransitionStates.IsValid(result.Single(), [New]).ShouldBeFalse();
    }

    [Test]
    public void Normalize_WhenIdIsArchivedAndALiveStateHasTheName_ShouldRebindWithTheNewId()
    {
        var archived = Active with { Archived = true };
        var readded = new TransitionState(Guid.NewGuid(), WorkItemTypeId.Story, "Active", false);

        var result = TransitionStates.Normalize([Transition(New, Active)], [New, archived, readded]);

        result.Single().To.ShouldBe(new StateRefJson(readded.Id, "Active"));
        result.Single().Id.ShouldBe("t1");
        TransitionStates.IsValid(result.Single(), [New, readded]).ShouldBeTrue();
    }

    [Test]
    public void Normalize_WhenAnotherTypeHasTheSameName_ShouldLeaveItInvalid()
    {
        var archived = Active with { Archived = true };
        var bugActive = new TransitionState(Guid.NewGuid(), WorkItemTypeId.Bug, "Active", false);

        var result = TransitionStates.Normalize([Transition(New, Active)], [New, archived, bugActive]);

        result.Single().To.StateId.ShouldBe(Active.Id);
    }

    [Test]
    public void Normalize_WhenTheNameIsCasedDifferently_ShouldRebindAndTakeTheLiveName()
    {
        var archived = Active with { Archived = true };
        var readded = new TransitionState(Guid.NewGuid(), WorkItemTypeId.Story, "ACTIVE", false);

        var result = TransitionStates.Normalize([Transition(New, Active)], [New, archived, readded]);

        result.Single().To.ShouldBe(new StateRefJson(readded.Id, "ACTIVE"));
    }

    [Test]
    public void Normalize_WhenFromIsArchivedAndReadded_ShouldRebindFromAndKeepAnyStateAsNull()
    {
        var archivedNew = New with { Archived = true };
        var readded = new TransitionState(Guid.NewGuid(), WorkItemTypeId.Story, "new", false);

        var result = TransitionStates.Normalize([Transition(New, Active), Transition(null, Active) with { Id = "t2" }], [archivedNew, readded, Active]);

        result[0].From.ShouldBe(new StateRefJson(readded.Id, "new"));
        result[1].From.ShouldBeNull();
    }

    [Test]
    public void Normalize_WhenRebindingWouldDuplicateAValidTransition_ShouldLeaveItInvalid()
    {
        var archived = Active with { Archived = true };
        var readded = new TransitionState(Guid.NewGuid(), WorkItemTypeId.Story, "Active", false);
        var valid = Transition(New, readded) with { Id = "valid" };
        var stale = Transition(New, Active) with { Id = "stale" };

        var result = TransitionStates.Normalize([valid, stale], [New, archived, readded]);

        result.Single(t => t.Id == "stale").To.StateId.ShouldBe(Active.Id);
    }

    [Test]
    public void Normalize_WhenRebindingWouldGoFromAStateToItself_ShouldLeaveItInvalid()
    {
        var archived = Active with { Archived = true };
        var archivedOld = New with { Archived = true, Name = "Active" };
        var readded = new TransitionState(Guid.NewGuid(), WorkItemTypeId.Story, "Active", false);

        var result = TransitionStates.Normalize([Transition(archivedOld, archived)], [archivedOld, archived, readded]);

        result.Single().To.StateId.ShouldBe(Active.Id);
    }

    [Test]
    public void Normalize_WhenRunTwice_ShouldChangeNothingMore()
    {
        var archived = Active with { Archived = true };
        var readded = new TransitionState(Guid.NewGuid(), WorkItemTypeId.Story, "active", false);
        var once = TransitionStates.Normalize([Transition(New, Active, toName: "x")], [New, archived, readded]);

        TransitionStates.Normalize(once, [New, archived, readded]).ShouldBe(once);
    }

    [Test]
    public void IsValid_WhenTheStateIsOfAnotherType_ShouldBeFalse()
    {
        var bugNew = New with { Type = WorkItemTypeId.Bug };

        TransitionStates.IsValid(Transition(null, New), [bugNew]).ShouldBeFalse();
    }
}
