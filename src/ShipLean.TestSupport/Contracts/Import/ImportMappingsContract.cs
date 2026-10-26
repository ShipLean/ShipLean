using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Import;
using Shouldly;

namespace ShipLean.TestSupport.Contracts.Import;

// Behaviour every IImportMappings must have: the suggested mapping and what it flags as new since an earlier run.
[TestFixture]
public abstract class ImportMappingsContract
{
    protected abstract IImportMappings Mappings { get; }

    protected abstract Task<MappingWorld> ArrangeAsync();

    [Test]
    public async Task Suggest_WhenPreviousJobGiven_ShouldFlagNewStatusesAndUsers()
    {
        var world = await ArrangeAsync();

        var mapping = await Mappings.SuggestAsync(world.ConnectionId, world.JiraProjectKey, world.PreviousJobId);

        mapping.Statuses.Where(s => s.IsNewSinceLastRun).Select(s => s.JiraStatus).ShouldBe([world.NewStatus]);
        mapping.Users.Where(u => u.IsNewSinceLastRun).Select(u => u.JiraAccountId).ShouldBe([world.NewUserAccountId]);
        mapping.Statuses.Count.ShouldBeGreaterThan(1);
        mapping.Users.Count.ShouldBeGreaterThan(1);
    }

    [Test]
    public async Task Suggest_WhenPreviousJobGiven_ShouldKeepItsChoices()
    {
        var world = await ArrangeAsync();

        var mapping = await Mappings.SuggestAsync(world.ConnectionId, world.JiraProjectKey, world.PreviousJobId);

        mapping.Fields.StoryPointsFieldId.ShouldBe(world.ChosenStoryPointsFieldId);
        mapping.Statuses.Single(s => s.JiraType == "Story" && s.JiraStatus == world.ChosenStatus).TargetState.ShouldBe(world.ChosenTargetState);
        var samePerson = mapping.Users.Single(u => u.JiraAccountId == world.SamePersonAccountId);
        samePerson.Resolution.ShouldBe(UserResolution.Existing);
        samePerson.User.ShouldNotBeNull().Id.ShouldBe(world.SamePersonUserId);
        samePerson.IsNewSinceLastRun.ShouldBeFalse();
    }

    [Test]
    public async Task Suggest_WhenNoPreviousJob_ShouldFlagNothing()
    {
        var world = await ArrangeAsync();

        var mapping = await Mappings.SuggestAsync(world.ConnectionId, world.JiraProjectKey, null);

        mapping.Statuses.ShouldAllBe(s => !s.IsNewSinceLastRun);
        mapping.Users.ShouldAllBe(u => !u.IsNewSinceLastRun);
        mapping.IssueTypes.ShouldAllBe(t => !t.IsNewSinceLastRun);
    }

    [Test]
    public async Task Suggest_WhenJiraProjectUnknown_ShouldBeNotFound()
    {
        var world = await ArrangeAsync();

        var error = await Should.ThrowAsync<ApiException>(() => Mappings.SuggestAsync(world.ConnectionId, "NOPE", null));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Suggest_WhenConnectionUnknown_ShouldBeNotFound()
    {
        var world = await ArrangeAsync();

        var error = await Should.ThrowAsync<ApiException>(() => Mappings.SuggestAsync(Guid.NewGuid(), world.JiraProjectKey, null));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Suggest_WhenPreviousJobUnknown_ShouldBeNotFound()
    {
        var world = await ArrangeAsync();

        var error = await Should.ThrowAsync<ApiException>(() => Mappings.SuggestAsync(world.ConnectionId, world.JiraProjectKey, Guid.NewGuid()));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }
}
