using System.Net;
using System.Net.Http.Json;
using Dapper;
using ShipLean.Contracts.Comments;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Sprints.Api;

namespace ShipLean.WebApi.Tests.WorkItemDetails.Comments;

// The comment endpoints through the real app. Joel is the project admin and the default caller; Mei is a plain member;
// Kai is in no project.
[TestFixture]
public class CommentApiTests : ApiFixture
{
    private static readonly string ListUrl = $"api/work-items/{SeedIds.WorkItems.Ship90}/comments";

    [Test]
    public async Task Supersede_WhenMember_ShouldStampAndKeepComment()
    {
        var comment = await AddAsync(Api, "Overtaken by events");
        var mei = Api.ActingAs(TestUser.Mei);
        using var watch = Watch();

        using var response = await mei.PostJsonAsync($"api/comments/{comment.Id}/supersede");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var superseded = (await response.Content.ReadFromJsonAsync<CommentDto>())!;
        superseded.Body.ShouldBe("Overtaken by events");
        var mark = superseded.Superseded.ShouldNotBeNull();
        mark.By.Id.ShouldBe(SeedIds.Users.Mei);
        mark.AtLabel.ShouldNotBeNullOrWhiteSpace();
        var listed = (await Api.GetJsonAsync<List<CommentDto>>(ListUrl)).Single(c => c.Id == comment.Id);
        listed.Superseded.ShouldNotBeNull().By.Id.ShouldBe(SeedIds.Users.Mei);
        (await watch.NextAsync<CommentsChanged>(e => e.WorkItemId == SeedIds.WorkItems.Ship90)).ProjectId.ShouldBe(SeedIds.Projects.Ship);
    }

    [Test]
    public async Task Supersede_WhenAlreadySuperseded_ShouldBeIdempotent()
    {
        var comment = await AddAsync(Api, "Marked twice");
        using var first = await Api.ActingAs(TestUser.Mei).PostJsonAsync($"api/comments/{comment.Id}/supersede");
        var firstMark = (await first.Content.ReadFromJsonAsync<CommentDto>())!.Superseded.ShouldNotBeNull();

        using var second = await Api.PostJsonAsync($"api/comments/{comment.Id}/supersede");

        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        var secondMark = (await second.Content.ReadFromJsonAsync<CommentDto>())!.Superseded.ShouldNotBeNull();
        secondMark.By.Id.ShouldBe(SeedIds.Users.Mei);
        secondMark.At.ShouldBe(firstMark.At);
    }

    [Test]
    public async Task Supersede_WhenNonMember_ShouldReturnNotFound()
    {
        var comment = await AddAsync(Api, "Not for outsiders");

        using var response = await Api.ActingAs(TestUser.Kai).PostJsonAsync($"api/comments/{comment.Id}/supersede");

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound);
        error.Code.ShouldBe(ApiErrorCodes.NotFound);
        await using var connection = await OpenDatabaseAsync();
        (await connection.ExecuteScalarAsync<bool>("SELECT superseded_at IS NULL FROM comment WHERE id = @id", new { id = comment.Id })).ShouldBeTrue();
    }

    [Test]
    public async Task Supersede_WhenCommentArchived_ShouldReturnNotFound()
    {
        var comment = await AddAsync(Api, "Gone already");
        using var archived = await Api.PostJsonAsync($"api/comments/{comment.Id}/archive");
        archived.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var response = await Api.PostJsonAsync($"api/comments/{comment.Id}/supersede");

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound);
        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Add_WhenMember_ShouldListNewestLastWithRenderedMarkdown()
    {
        var first = await AddAsync(Api.ActingAs(TestUser.Mei), "First **bold**");
        var second = await AddAsync(Api.ActingAs(TestUser.Mei), "Second");

        var listed = await Api.GetJsonAsync<List<CommentDto>>(ListUrl);

        listed.Select(c => c.Id).ShouldContain(first.Id);
        listed.IndexOf(listed.Single(c => c.Id == first.Id)).ShouldBeLessThan(listed.IndexOf(listed.Single(c => c.Id == second.Id)));
        first.BodyHtml.ShouldContain("<strong>bold</strong>");
        first.Author.ShouldNotBeNull().Id.ShouldBe(SeedIds.Users.Mei);
    }

    [Test]
    public async Task Add_WhenBlank_ShouldBeValidation()
    {
        using var response = await Api.PostJsonAsync(ListUrl, new CommentCreate("  "));

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Comments_WhenNotMember_ShouldBeNotFound()
    {
        var comment = await AddAsync(Api, "Members only");
        var kai = Api.ActingAs(TestUser.Kai);

        using var list = await kai.GetAsync(ListUrl);
        using var add = await kai.PostJsonAsync(ListUrl, new CommentCreate("Hello"));
        using var edit = await kai.PatchJsonAsync($"api/comments/{comment.Id}", new CommentEdit("Hijacked"));
        using var archive = await kai.PostJsonAsync($"api/comments/{comment.Id}/archive");

        foreach (var response in new[] { list, add, edit, archive })
        {
            var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound);
            error.Code.ShouldBe(ApiErrorCodes.NotFound);
        }

        (await StoredBodyAsync(comment.Id)).ShouldBe("Members only");
    }

    [Test]
    public async Task Edit_WhenAuthor_ShouldChangeTheBody()
    {
        var mei = Api.ActingAs(TestUser.Mei);
        var comment = await AddAsync(mei, "Before");

        using var response = await mei.PatchJsonAsync($"api/comments/{comment.Id}", new CommentEdit("After"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var edited = (await response.Content.ReadFromJsonAsync<CommentDto>())!;
        edited.Body.ShouldBe("After");
        edited.EditedLabel.ShouldNotBeNull();
    }

    // The archive is held uncommitted while the edit runs, so the edit's statement waits on the row lock and finds the comment
    // archived when it resumes: it must answer not_found, as an edit of an archived comment does.
    [Test]
    public async Task Edit_WhenArchivedWhileTheEditWaits_ShouldBeNotFound()
    {
        var mei = Api.ActingAs(TestUser.Mei);
        var comment = await AddAsync(mei, "Racing");
        await using var archiver = await OpenDatabaseAsync();
        await using var transaction = await archiver.BeginTransactionAsync();
        await archiver.ExecuteAsync("UPDATE comment SET archived_at = now() WHERE id = @id", new { id = comment.Id }, transaction);

        var edit = mei.PatchJsonAsync($"api/comments/{comment.Id}", new CommentEdit("Too late"));
        await using (var watcher = await OpenDatabaseAsync())
        {
            await LockWaiters.WaitForOneAsync(watcher);
        }

        await transaction.CommitAsync();

        using var response = await edit;
        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
        (await StoredBodyAsync(comment.Id)).ShouldBe("Racing");
    }

    [Test]
    public async Task Edit_WhenAnotherUser_ShouldBeForbidden()
    {
        var comment = await AddAsync(Api.ActingAs(TestUser.Mei), "Mei's words");

        using var response = await Api.PatchJsonAsync($"api/comments/{comment.Id}", new CommentEdit("Joel is an admin but not the author"));

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Forbidden);
        error.Code.ShouldBe(ApiErrorCodes.Forbidden);
        (await StoredBodyAsync(comment.Id)).ShouldBe("Mei's words");
    }

    [Test]
    public async Task Archive_WhenProjectAdmin_ShouldHideItAndKeepTheRow()
    {
        var comment = await AddAsync(Api.ActingAs(TestUser.Mei), "Admin removes this");

        using var response = await Api.PostJsonAsync($"api/comments/{comment.Id}/archive");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Api.GetJsonAsync<List<CommentDto>>(ListUrl)).ShouldNotContain(c => c.Id == comment.Id);
        await using var connection = await OpenDatabaseAsync();
        (await connection.ExecuteScalarAsync<bool>("SELECT archived_at IS NOT NULL FROM comment WHERE id = @id", new { id = comment.Id })).ShouldBeTrue();
    }

    [Test]
    public async Task Archive_WhenAuthorWhoIsNotAdmin_ShouldHideIt()
    {
        var mei = Api.ActingAs(TestUser.Mei);
        var comment = await AddAsync(mei, "Mine to remove");

        using var response = await mei.PostJsonAsync($"api/comments/{comment.Id}/archive");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task Archive_WhenMemberIsNotAuthorOrAdmin_ShouldBeForbidden()
    {
        var comment = await AddAsync(Api, "Joel's words");

        using var response = await Api.ActingAs(TestUser.Mei).PostJsonAsync($"api/comments/{comment.Id}/archive");

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Forbidden);
        error.Code.ShouldBe(ApiErrorCodes.Forbidden);
        (await Api.GetJsonAsync<List<CommentDto>>(ListUrl)).ShouldContain(c => c.Id == comment.Id);
    }

    [Test]
    public async Task Add_WhenSaved_ShouldPublishCommentsChangedForTheWorkItem()
    {
        using var watch = Watch();

        await AddAsync(Api, "Heard by others");

        var published = await watch.NextAsync<CommentsChanged>(e => e.WorkItemId == SeedIds.WorkItems.Ship90);
        published.ProjectId.ShouldBe(SeedIds.Projects.Ship);
    }

    [Test]
    public async Task Edit_WhenSaved_ShouldPublishCommentsChanged()
    {
        var comment = await AddAsync(Api, "Before");
        using var watch = Watch();

        using var response = await Api.PatchJsonAsync($"api/comments/{comment.Id}", new CommentEdit("After"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await watch.NextAsync<CommentsChanged>(e => e.WorkItemId == SeedIds.WorkItems.Ship90)).ProjectId.ShouldBe(SeedIds.Projects.Ship);
    }

    [Test]
    public async Task Archive_WhenArchived_ShouldPublishCommentsChanged()
    {
        var comment = await AddAsync(Api, "To go");
        using var watch = Watch();

        using var response = await Api.PostJsonAsync($"api/comments/{comment.Id}/archive");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await watch.NextAsync<CommentsChanged>(e => e.WorkItemId == SeedIds.WorkItems.Ship90)).ProjectId.ShouldBe(SeedIds.Projects.Ship);
    }

    [Test]
    public async Task Edit_WhenRefused_ShouldPublishNothing()
    {
        var comment = await AddAsync(Api.ActingAs(TestUser.Mei), "Mei's");
        using var watch = Watch();

        using var refused = await Api.PatchJsonAsync($"api/comments/{comment.Id}", new CommentEdit("No"));
        refused.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        await AddAsync(Api, "Marker");

        // The marker's own event is the first one after the refusal: nothing arrived in between.
        var next = await watch.NextAsync<CommentsChanged>(_ => true);
        next.WorkItemId.ShouldBe(SeedIds.WorkItems.Ship90);
        (await StoredBodyAsync(comment.Id)).ShouldBe("Mei's");
    }

    private ProjectEventWatch Watch() => new(Host.Services.GetRequiredService<IProjectEvents>(), SeedIds.Projects.Ship);

    private async Task<string?> StoredBodyAsync(Guid id)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.ExecuteScalarAsync<string?>("SELECT body FROM comment WHERE id = @id", new { id });
    }

    private static async Task<CommentDto> AddAsync(ApiClient api, string body)
    {
        using var response = await api.PostJsonAsync(ListUrl, new CommentCreate(body));
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<CommentDto>())!;
    }
}
