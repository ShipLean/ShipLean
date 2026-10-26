using ShipLean.Contracts;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Projects;
using ShipLean.Contracts.Sprints;
using Shouldly;

namespace ShipLean.TestSupport.Contracts.Projects;

// Behaviour every IProjectFactory must have, whatever it stores data in.
[TestFixture]
public abstract class ProjectFactoryContract
{
    // The factory acting as the given user (the fake reads the id from its current user; Postgres derivations sign the actor in).
    protected abstract IProjectFactory CreateFactory(Guid actorId);

    protected abstract Task<Guid> ArrangeUserAsync(string email);

    protected abstract Task<Guid> ArrangeProjectAsync(string key);

    protected abstract Task<IReadOnlyList<(Guid UserId, ProjectRole Role)>> ReadMembersAsync(Guid projectId);

    protected abstract Task<int> CountSprintsAsync(Guid projectId);

    // A project with the default states, one extra Story state named stateName, and one work item.
    protected abstract Task<Guid> ArrangeSourceProjectAsync(string key, string stateName);

    // The live states as (type, name).
    protected abstract Task<IReadOnlyList<(WorkItemTypeId Type, string Name)>> ReadStatesAsync(Guid projectId);

    protected abstract Task<int> CountItemsAsync(Guid projectId);

    private static string NewKey() => new(Enumerable.Range(0, 4).Select(_ => (char)('A' + Random.Shared.Next(26))).ToArray());

    private static ProjectCreate Create(string key, IReadOnlyList<string>? emails = null, SprintCreate? sprint = null, Guid? copyFrom = null)
        => new($"Project {key}", key, copyFrom, emails ?? [], [1, 2, 3, 4, 5], sprint);

    [Test]
    public async Task Create_ShouldMakeTheCreatorAdminAndAddTheMembers()
    {
        var creator = await ArrangeUserAsync($"{Guid.NewGuid():N}@example.test");
        var email = $"{Guid.NewGuid():N}@example.test";
        var member = await ArrangeUserAsync(email);

        var project = await CreateFactory(creator).CreateAsync(Create(NewKey(), [email]));

        project.Role.ShouldBe(ProjectRole.Admin);
        var members = await ReadMembersAsync(project.Id);
        members.ShouldContain((creator, ProjectRole.Admin));
        members.ShouldContain((member, ProjectRole.Member));
        project.WorkingDays.ShouldBe([1, 2, 3, 4, 5]);
    }

    [Test]
    public async Task Create_WhenKeyTaken_ShouldBeConflict()
    {
        var creator = await ArrangeUserAsync($"{Guid.NewGuid():N}@example.test");
        var key = NewKey();
        await ArrangeProjectAsync(key);

        var error = await Should.ThrowAsync<ApiException>(() => CreateFactory(creator).CreateAsync(Create(key)));

        error.Code.ShouldBe(ApiErrorCodes.Conflict);
    }

    [Test]
    public async Task Create_WhenKeyInvalid_ShouldBeValidation()
    {
        var creator = await ArrangeUserAsync($"{Guid.NewGuid():N}@example.test");

        var error = await Should.ThrowAsync<ApiException>(() => CreateFactory(creator).CreateAsync(Create("ab1")));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Create_WhenMemberEmailUnknown_ShouldBeNotFound()
    {
        var creator = await ArrangeUserAsync($"{Guid.NewGuid():N}@example.test");

        var error = await Should.ThrowAsync<ApiException>(() => CreateFactory(creator).CreateAsync(Create(NewKey(), ["nobody@example.test"])));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Create_WhenFirstSprintGiven_ShouldCreateIt()
    {
        var creator = await ArrangeUserAsync($"{Guid.NewGuid():N}@example.test");
        var today = DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime);

        var project = await CreateFactory(creator).CreateAsync(Create(NewKey(), sprint: new SprintCreate("Sprint 1", today, today.AddDays(13), "")));

        (await CountSprintsAsync(project.Id)).ShouldBe(1);
    }

    [Test]
    public async Task Create_WhenNoFirstSprint_ShouldCreateNone()
    {
        var creator = await ArrangeUserAsync($"{Guid.NewGuid():N}@example.test");

        var project = await CreateFactory(creator).CreateAsync(Create(NewKey()));

        (await CountSprintsAsync(project.Id)).ShouldBe(0);
    }

    [Test]
    public async Task Create_WhenCopiedFrom_ShouldCopyStatesNotItems()
    {
        var creator = await ArrangeUserAsync($"{Guid.NewGuid():N}@example.test");
        var source = await ArrangeSourceProjectAsync(NewKey(), "Needs design");
        var factory = CreateFactory(creator);

        var copy = await factory.CreateAsync(Create(NewKey(), copyFrom: source));

        var copied = await ReadStatesAsync(copy.Id);
        copied.ShouldContain((WorkItemTypeId.Story, "Needs design"));
        copied.Order().ShouldBe((await ReadStatesAsync(source)).Order());
        (await CountItemsAsync(source)).ShouldBe(1);
        (await CountItemsAsync(copy.Id)).ShouldBe(0);
    }

    [Test]
    public async Task CheckKey_WhenTaken_ShouldSayWhoseProjectItIs()
    {
        var creator = await ArrangeUserAsync($"{Guid.NewGuid():N}@example.test");
        var stranger = await ArrangeUserAsync($"{Guid.NewGuid():N}@example.test");
        var key = NewKey();
        await CreateFactory(creator).CreateAsync(Create(key));

        var mine = await CreateFactory(creator).CheckKeyAsync(key, "Anything");
        var theirs = await CreateFactory(stranger).CheckKeyAsync(key, "Anything");

        mine.Available.ShouldBeFalse();
        mine.Reason.ShouldBe(KeyCheckReason.Taken);
        theirs.Reason.ShouldBe(KeyCheckReason.TakenByAnotherProject);
        theirs.Alternatives.ShouldNotBeEmpty();
        theirs.Alternatives.ShouldNotContain(key);
    }

    [Test]
    public async Task CheckKey_WhenFreeOrInvalid_ShouldSayAvailableOrInvalid()
    {
        var user = await ArrangeUserAsync($"{Guid.NewGuid():N}@example.test");

        var free = await CreateFactory(user).CheckKeyAsync(NewKey(), "Anything");
        var invalid = await CreateFactory(user).CheckKeyAsync("x", "Anything");

        free.Available.ShouldBeTrue();
        free.Reason.ShouldBeNull();
        invalid.Available.ShouldBeFalse();
        invalid.Reason.ShouldBe(KeyCheckReason.Invalid);
    }
}
