using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Identity;

// IProjectAccess on the seed. A test acts as a user through Host.ActAs, as a service called outside a request does.
[TestFixture]
public class ProjectAccessTests : ApiFixture
{
    private static readonly Guid Comment = new("0000000b-0000-4000-8001-000000000001");
    private static readonly Guid Attachment = new("0000000c-0000-4000-8001-000000000001");

    private IProjectAccess Access => Host.Services.GetRequiredService<IProjectAccess>();

    [OneTimeSetUp]
    public async Task ArrangeACommentAndAnAttachment()
    {
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync(
            "INSERT INTO comment (id, work_item_id, author_id, body) VALUES (@Comment, @item, @joel, 'Looks right.')",
            new { Comment, item = SeedIds.WorkItems.Ship90, joel = SeedIds.Users.Joel });
        await connection.ExecuteAsync(
            "INSERT INTO attachment (id, work_item_id, name, content_type, size_bytes, storage_ref) VALUES (@Attachment, @item, 'a.png', 'image/png', 3, 'blob/a')",
            new { Attachment, item = SeedIds.WorkItems.Ship90 });
    }

    [Test]
    public async Task ProjectAccess_WhenNotMember_ShouldThrowNotFound()
    {
        Host.ActAs(SeedIds.Users.Kai);

        var notMember = await Should.ThrowAsync<ApiException>(() => Access.RequireAsync(SeedIds.Projects.Ship, ProjectRole.Member));
        var unknown = await Should.ThrowAsync<ApiException>(() => Access.RequireAsync(Guid.NewGuid(), ProjectRole.Member));

        notMember.Code.ShouldBe(ApiErrorCodes.NotFound);
        unknown.Code.ShouldBe(ApiErrorCodes.NotFound);
        notMember.Message.ShouldBe(unknown.Message);
    }

    [Test]
    public async Task ProjectAccess_WhenInstanceAdminIsNotMember_ShouldStillThrowNotFound()
    {
        Host.ActAs(SeedIds.Users.Tom);

        var error = await Should.ThrowAsync<ApiException>(() => Access.RequireAsync(SeedIds.Projects.Empt, ProjectRole.Member));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task ProjectAccess_WhenMemberButAdminRequired_ShouldThrowForbidden()
    {
        Host.ActAs(SeedIds.Users.Priya);

        var role = await Access.RequireAsync(SeedIds.Projects.Ship, ProjectRole.Member);
        var error = await Should.ThrowAsync<ApiException>(() => Access.RequireAsync(SeedIds.Projects.Ship, ProjectRole.Admin));

        role.ShouldBe(ProjectRole.Member);
        error.Code.ShouldBe(ApiErrorCodes.Forbidden);
    }

    [Test]
    public async Task ProjectAccess_WhenAdmin_ShouldReturnAdmin()
    {
        Host.ActAs(SeedIds.Users.Joel);

        (await Access.RequireAsync(SeedIds.Projects.Ship, ProjectRole.Admin)).ShouldBe(ProjectRole.Admin);
    }

    [Test]
    public async Task ProjectAccess_WhenMembershipArchived_ShouldThrowNotFound()
    {
        await using (var connection = await OpenDatabaseAsync())
        {
            await connection.ExecuteAsync("UPDATE project_member SET archived_at = now() WHERE id = @id", new { id = SeedIds.ProjectMembers.ShipPriya });
        }
        Host.ActAs(SeedIds.Users.Priya);

        var error = await Should.ThrowAsync<ApiException>(() => Access.RequireAsync(SeedIds.Projects.Ship, ProjectRole.Member));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
        await using var restore = await OpenDatabaseAsync();
        await restore.ExecuteAsync("UPDATE project_member SET archived_at = NULL WHERE id = @id", new { id = SeedIds.ProjectMembers.ShipPriya });
    }

    [TestCaseSource(nameof(Items))]
    public async Task ProjectAccess_WhenItemIdGiven_ShouldResolveProject(ProjectItem item, Guid id, Guid projectId)
    {
        Host.ActAs(SeedIds.Users.Joel);

        var (resolved, role) = await Access.RequireAsync(item, id, ProjectRole.Member);

        resolved.ShouldBe(projectId);
        role.ShouldNotBe(default);
    }

    [TestCaseSource(nameof(Items))]
    public async Task ProjectAccess_WhenItemIdGivenByANonMember_ShouldThrowNotFound(ProjectItem item, Guid id, Guid projectId)
    {
        projectId.ShouldNotBe(Guid.Empty);
        Host.ActAs(SeedIds.Users.Kai);

        var error = await Should.ThrowAsync<ApiException>(() => Access.RequireAsync(item, id, ProjectRole.Member));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [TestCase(ProjectItem.WorkItem)]
    [TestCase(ProjectItem.Sprint)]
    [TestCase(ProjectItem.Board)]
    [TestCase(ProjectItem.Comment)]
    [TestCase(ProjectItem.Attachment)]
    [TestCase(ProjectItem.ImportJob)]
    [TestCase(ProjectItem.JiraConnection)]
    [TestCase(ProjectItem.Link)]
    public async Task ProjectAccess_WhenItemIdUnknown_ShouldThrowNotFound(ProjectItem item)
    {
        Host.ActAs(SeedIds.Users.Joel);

        var error = await Should.ThrowAsync<ApiException>(() => Access.RequireAsync(item, Guid.NewGuid(), ProjectRole.Member));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task ProjectAccess_WhenItemInProjectWhereOnlyMember_ShouldRefuseAdminWithForbidden()
    {
        Host.ActAs(SeedIds.Users.Priya);

        var error = await Should.ThrowAsync<ApiException>(() => Access.RequireAsync(ProjectItem.Sprint, SeedIds.Sprints.Sprint24, ProjectRole.Admin));

        error.Code.ShouldBe(ApiErrorCodes.Forbidden);
    }

    [Test]
    public async Task RequireInstanceAdmin_WhenNotAdmin_ShouldThrowForbidden()
    {
        Host.ActAs(SeedIds.Users.Priya);
        var error = await Should.ThrowAsync<ApiException>(() => Access.RequireInstanceAdminAsync());
        error.Code.ShouldBe(ApiErrorCodes.Forbidden);

        Host.ActAs(SeedIds.Users.Tom);
        await Should.NotThrowAsync(() => Access.RequireInstanceAdminAsync());
    }

    [Test]
    public void CurrentUser_WhenNoUser_ShouldThrow()
    {
        Host.ActAs(null);

        Should.Throw<InvalidOperationException>(() => Host.Services.GetRequiredService<ICurrentUser>().Id);
    }

    private static IEnumerable<TestCaseData> Items()
    {
        yield return Case(ProjectItem.WorkItem, SeedIds.WorkItems.Ship90, SeedIds.Projects.Ship);
        yield return Case(ProjectItem.Sprint, SeedIds.Sprints.Sprint24, SeedIds.Projects.Ship);
        yield return Case(ProjectItem.Board, SeedIds.Boards.ShipStories, SeedIds.Projects.Ship);
        yield return Case(ProjectItem.Comment, Comment, SeedIds.Projects.Ship);
        yield return Case(ProjectItem.Attachment, Attachment, SeedIds.Projects.Ship);
        yield return Case(ProjectItem.ImportJob, SeedIds.ImportJobs.MobImport, SeedIds.Projects.Mob);
        yield return Case(ProjectItem.JiraConnection, SeedIds.JiraConnections.Mob, SeedIds.Projects.Mob);
        yield return Case(ProjectItem.Link, SeedIds.Links.Ship101Pull, SeedIds.Projects.Ship);
    }

    private static TestCaseData Case(ProjectItem item, Guid id, Guid project) => new TestCaseData(item, id, project).SetArgDisplayNames(item.ToString());
}
