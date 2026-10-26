using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Features.Mcp.Resolve;

namespace ShipLean.WebApi.Tests.Mcp.Resolve;

// The MCP resolvers on the seed, called from the host's container as Joel (a member of SHIP, PAY, MOB, DATA, OPS and EMPT, not PART); nothing here changes the database.
[TestFixture]
public class ResolveTests : ApiFixture
{
    private string _partItemKey = string.Empty;

    private ProjectResolver Projects => Host.Services.GetRequiredService<ProjectResolver>();

    private ItemResolver Items => Host.Services.GetRequiredService<ItemResolver>();

    private SprintResolver Sprints => Host.Services.GetRequiredService<SprintResolver>();

    private StateResolver States => Host.Services.GetRequiredService<StateResolver>();

    private MemberResolver Members => Host.Services.GetRequiredService<MemberResolver>();

    [OneTimeSetUp]
    public async Task ArrangeAnItemInAProjectJoelIsNotIn()
    {
        await using var connection = await OpenDatabaseAsync();
        var id = await new WorkItemBuilder(SeedIds.Projects.Part).InsertAsync(connection);
        _partItemKey = await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @id", new { id });
        await connection.ExecuteAsync(
            "INSERT INTO sprint (id, project_id, name, start_date, end_date) VALUES (@id, @project, 'Partner Sprint', DATE '2020-01-01', DATE '2020-01-14')",
            new { id = Guid.NewGuid(), project = SeedIds.Projects.Part });
    }

    [Test]
    public async Task Resolve_WhenKeyGivenInAnyCase_ShouldFindTheProjectWithRole()
    {
        var project = await Projects.ResolveAsync(" ship ", forWrite: true);

        project.ShouldBe(new ResolvedProject(SeedIds.Projects.Ship, "SHIP", project.Name, ProjectRole.Admin, true));
    }

    [Test]
    public async Task Resolve_WhenForeignKey_ShouldBeNotFound()
    {
        var unknown = await Should.ThrowAsync<ApiException>(() => Projects.ResolveAsync("ZZZZ", forWrite: false));
        var foreignProject = await Should.ThrowAsync<ApiException>(() => Projects.ResolveAsync("PART", forWrite: false));
        var foreignItem = await Should.ThrowAsync<ApiException>(() => Items.ResolveAsync(_partItemKey, ProjectRole.Member));
        var missingItem = await Should.ThrowAsync<ApiException>(() => Items.ResolveAsync("SHIP-99999", ProjectRole.Member));

        new[] { unknown, foreignProject, foreignItem, missingItem }.ShouldAllBe(e => e.Code == ApiErrorCodes.NotFound);
        foreignProject.Message.ShouldBe(unknown.Message);
        foreignItem.Message.ShouldBe(missingItem.Message);
    }

    [Test]
    public async Task Resolve_WhenWriteWithoutProjectAndTwoProjects_ShouldBeValidation()
    {
        var error = await Should.ThrowAsync<ApiException>(() => Projects.ResolveAsync(null, forWrite: true));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("project");
        error.Message.ShouldContain("name a project");
    }

    [Test]
    public async Task Resolve_WhenWriteWithoutProjectAndOneProject_ShouldUseIt()
    {
        Host.ActAs(SeedIds.Users.Ravi);

        var project = await Projects.ResolveAsync(null, forWrite: true);

        project.Id.ShouldBe(SeedIds.Projects.Pay);
    }

    [Test]
    public async Task Resolve_WhenNoProjectAtAll_ShouldBeNotFound()
    {
        Host.ActAs(SeedIds.Users.Kai);

        var error = await Should.ThrowAsync<ApiException>(() => Projects.ResolveAsync(null, forWrite: false));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Resolve_WhenReadWithoutProjectAndNoRecent_ShouldUseFirstByKey()
    {
        var project = await Projects.ResolveAsync(null, forWrite: false);

        project.Key.ShouldBe("DATA");
    }

    [Test]
    public async Task Resolve_WhenItemKeyInAnotherProject_ShouldBeValidation()
    {
        var error = await Should.ThrowAsync<ApiException>(() => Items.ResolveAsync("SHIP-90", ProjectRole.Member, SeedIds.Projects.Pay, "parent"));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("parent");
    }

    [Test]
    public async Task Resolve_WhenItemKeyLowerCase_ShouldFindIt()
    {
        var item = await Items.ResolveAsync("ship-90", ProjectRole.Member, SeedIds.Projects.Ship);

        item.ShouldBe(new ResolvedItem(SeedIds.WorkItems.Ship90, "SHIP-90", SeedIds.Projects.Ship, ProjectRole.Admin, true));
    }

    [Test]
    public async Task Resolve_WhenItemKeyMalformed_ShouldBeValidation()
    {
        var error = await Should.ThrowAsync<ApiException>(() => Items.ResolveAsync("ship 90", ProjectRole.Member));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("item");
    }

    [Test]
    public async Task Resolve_WhenSprintCurrent_ShouldBeSprint24()
    {
        var sprint = await Sprints.ResolveAsync(SeedIds.Projects.Ship, "Current");

        sprint.ShouldBe(new ResolvedSprint(SeedIds.Sprints.Sprint24, "Sprint 24", true));
    }

    [Test]
    public async Task Resolve_WhenSprintNext_ShouldBeSprint25()
    {
        var sprint = await Sprints.ResolveAsync(SeedIds.Projects.Ship, "next");

        sprint.Id.ShouldBe(SeedIds.Sprints.Sprint25);
    }

    [Test]
    public async Task Resolve_WhenNoSprintIsCurrent_ShouldBeNotFoundSayingSo()
    {
        var error = await Should.ThrowAsync<ApiException>(() => Sprints.ResolveAsync(SeedIds.Projects.Pay, "current"));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
        error.Message.ShouldContain("No sprint is running");
    }

    [Test]
    public async Task Resolve_WhenSprintNameAmbiguous_ShouldListCandidates()
    {
        var error = await Should.ThrowAsync<ApiException>(() => Sprints.ResolveAsync(SeedIds.Projects.Ship, "Sprint 2"));

        error.Code.ShouldBe(ApiErrorCodes.Ambiguous);
        error.Candidates.ShouldBe(["Sprint 20", "Sprint 21", "Sprint 22", "Sprint 23", "Sprint 24", "Sprint 25", "Sprint 26"]);
    }

    [Test]
    public async Task Resolve_WhenNameAmbiguous_ShouldListCandidates()
    {
        var error = await Should.ThrowAsync<ApiException>(() => Members.ResolveAsync(SeedIds.Projects.Ship, "s"));

        error.Code.ShouldBe(ApiErrorCodes.Ambiguous);
        error.Field.ShouldBe("assignee");
        error.Candidates!.Count.ShouldBe(2);
        error.Candidates[0].ShouldStartWith("Priya Shah <");
        error.Candidates[1].ShouldStartWith("Sam Rivera <");
    }

    [Test]
    public async Task Resolve_WhenStateExistsForTaskOnly_ShouldBeAmbiguousForStory()
    {
        var error = await Should.ThrowAsync<ApiException>(() => States.ResolveAsync(SeedIds.Projects.Ship, WorkItemTypeId.Story, "In review"));

        error.Code.ShouldBe(ApiErrorCodes.Ambiguous);
        error.Field.ShouldBe("state");
        error.Candidates.ShouldBe(["Active", "Closed", "New", "Removed", "Resolved"]);
    }

    [Test]
    public async Task Resolve_WhenStateExistsForItsType_ShouldFindIt()
    {
        var task = await States.ResolveAsync(SeedIds.Projects.Ship, WorkItemTypeId.Task, "in review");
        var bug = await States.ResolveAsync(SeedIds.Projects.Ship, WorkItemTypeId.Bug, "Triaged");

        task.Id.ShouldBe(SeedIds.States.Ship.TaskInReview);
        bug.ShouldBe(new ResolvedState(SeedIds.States.Ship.BugTriaged, "Triaged", WorkItemTypeId.Bug, bug.Category, true));
    }

    [Test]
    public async Task Resolve_WhenNoTypeHasTheState_ShouldBeNotFound()
    {
        var error = await Should.ThrowAsync<ApiException>(() => States.ResolveAsync(SeedIds.Projects.Ship, WorkItemTypeId.Story, "Nonsense"));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Resolve_WhenSprintInForeignProject_ShouldBeNotFound()
    {
        var error = await Should.ThrowAsync<ApiException>(() => Sprints.ResolveAsync(SeedIds.Projects.Part, "Partner Sprint"));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Resolve_WhenStateInForeignProject_ShouldBeNotFound()
    {
        var error = await Should.ThrowAsync<ApiException>(() => States.ResolveAsync(SeedIds.Projects.Part, WorkItemTypeId.Story, "New"));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Resolve_WhenMemberInForeignProject_ShouldBeNotFound()
    {
        var error = await Should.ThrowAsync<ApiException>(() => Members.ResolveAsync(SeedIds.Projects.Part, "priya"));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Resolve_WhenStateNull_ShouldBeValidation()
    {
        var error = await Should.ThrowAsync<ApiException>(() => States.ResolveAsync(SeedIds.Projects.Ship, WorkItemTypeId.Story, null!));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("state");
    }

    [Test]
    public async Task Resolve_WhenSprintOrMemberOrItemNull_ShouldBeValidation()
    {
        var sprint = await Should.ThrowAsync<ApiException>(() => Sprints.ResolveAsync(SeedIds.Projects.Ship, null!));
        var member = await Should.ThrowAsync<ApiException>(() => Members.ResolveAsync(SeedIds.Projects.Ship, null!));
        var item = await Should.ThrowAsync<ApiException>(() => Items.ResolveAsync(null!, ProjectRole.Member));

        new[] { sprint, member, item }.ShouldAllBe(e => e.Code == ApiErrorCodes.Validation);
        new[] { sprint.Field, member.Field, item.Field }.ShouldBe(["sprint", "assignee", "item"]);
    }

    [Test]
    public async Task Resolve_WhenNamePrefixed_ShouldNotBeExact()
    {
        var exact = await Sprints.ResolveAsync(SeedIds.Projects.Ship, "Sprint 24");
        var member = await Members.ResolveAsync(SeedIds.Projects.Ship, "priya");
        var state = await States.ResolveAsync(SeedIds.Projects.Ship, WorkItemTypeId.Task, "in rev");

        exact.Exact.ShouldBeTrue();
        member.Exact.ShouldBeFalse();
        state.Exact.ShouldBeFalse();
    }

    [Test]
    public async Task Resolve_WhenMemberCandidateGivenBack_ShouldResolveToThatMember()
    {
        var ambiguous = await Should.ThrowAsync<ApiException>(() => Members.ResolveAsync(SeedIds.Projects.Ship, "s"));

        var back = await Members.ResolveAsync(SeedIds.Projects.Ship, ambiguous.Candidates![0]);

        back.Name.ShouldBe("Priya Shah");
        back.Exact.ShouldBeTrue();
    }

    [Test]
    public async Task Resolve_WhenMemberMe_ShouldBeCurrentUser()
    {
        var me = await Members.ResolveAsync(SeedIds.Projects.Ship, "me");

        me.Id.ShouldBe(SeedIds.Users.Joel);
    }

    [Test]
    public async Task Resolve_WhenMemberByEmailNameOrInitials_ShouldFindTheSamePerson()
    {
        var byEmail = await Members.ResolveAsync(SeedIds.Projects.Ship, " Priya@ShipLean.local ");
        var byInitials = await Members.ResolveAsync(SeedIds.Projects.Ship, "ps");
        var byName = await Members.ResolveAsync(SeedIds.Projects.Ship, "priya shah");
        var byWord = await Members.ResolveAsync(SeedIds.Projects.Ship, "Okafor");

        new[] { byEmail.Id, byInitials.Id, byName.Id }.ShouldAllBe(id => id == SeedIds.Users.Priya);
        byWord.Id.ShouldBe(SeedIds.Users.Tom);
    }

    [Test]
    public async Task Resolve_WhenMemberNotInProject_ShouldBeNotFound()
    {
        var error = await Should.ThrowAsync<ApiException>(() => Members.ResolveAsync(SeedIds.Projects.Ship, "kai@shiplean.local"));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }
}
