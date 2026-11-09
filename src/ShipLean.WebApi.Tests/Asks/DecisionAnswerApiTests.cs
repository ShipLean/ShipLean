using System.Net;
using System.Net.Http.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Comments;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Usage;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Asks;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Usage;
using ShipLean.WebApi.Tests.Sprints.Api;

namespace ShipLean.WebApi.Tests.Asks;

// The decision routes through the real app. Joel is the project admin and the default caller; Mei is a plain member; Kai is in no
// project. Every test arranges its own work item and decisions, and asserts stored state through AskRows, never through GET asks.
[TestFixture]
public class DecisionAnswerApiTests : ApiFixture
{
    private RecordingUsageEvents Recorder => (RecordingUsageEvents)Host.Services.GetRequiredService<IUsageEvents>();

    [SetUp]
    public void ClearUsage() => Recorder.Clear();

    [Test]
    public async Task Confirm_WhenOpen_ShouldConfirmAndKeepRecommended()
    {
        var item = await NewItemAsync();
        var (id, _) = await NewDecisionAsync(item, 1);

        using var response = await Api.PostJsonAsync(Url(item, "confirm"), new { numbers = new[] { 1 } });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
        await using var connection = await OpenDatabaseAsync();
        var decision = await AskRows.DecisionAsync(connection, id);
        (decision.State, decision.CurrentAnswer, decision.AnswerKind, decision.AnsweredByUserId).ShouldBe(("confirmed", "Yes", "recommended", SeedIds.Users.Joel));
        decision.AnsweredAt.ShouldNotBeNull();
        var confirmed = (await AskRows.EventsAsync(connection, item)).Single(e => e.Kind == "confirmed");
        (confirmed.DecisionId, confirmed.ToAnswer, confirmed.ToKind, confirmed.ByUserId).ShouldBe((id, "Yes", "recommended", SeedIds.Users.Joel));
    }

    [Test]
    public async Task Confirm_WhenMemberIsNotOwner_ShouldConfirm()
    {
        var item = await NewItemAsync();
        var (id, _) = await NewDecisionAsync(item, 1);

        using var response = await Api.ActingAs(TestUser.Mei).PostJsonAsync(Url(item, "confirm"), new { numbers = new[] { 1 } });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
        await using var connection = await OpenDatabaseAsync();
        var decision = await AskRows.DecisionAsync(connection, id);
        (decision.State, decision.AnsweredByUserId).ShouldBe(("confirmed", SeedIds.Users.Mei));
    }

    [Test]
    public async Task Confirm_WhenAlreadyConfirmed_ShouldBeConflict()
    {
        var item = await NewItemAsync();
        await NewDecisionAsync(item, 1, "confirmed");

        using var response = await Api.PostJsonAsync(Url(item, "confirm"), new { numbers = new[] { 1 } });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Conflict)).Code.ShouldBe(ApiErrorCodes.Conflict);
        await using var connection = await OpenDatabaseAsync();
        (await AskRows.EventsAsync(connection, item)).ShouldNotContain(e => e.Kind == "confirmed");
    }

    [Test]
    public async Task ConfirmMany_WhenSomeAreNotOpen_ShouldConfirmOnlyOpen()
    {
        var item = await NewItemAsync();
        await NewDecisionAsync(item, 1);
        await NewDecisionAsync(item, 2, "withdrawn");
        await NewDecisionAsync(item, 3);

        using var response = await Api.PostJsonAsync(Url(item, "confirm"), new { numbers = new[] { 1, 2, 3 } });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
        await using var connection = await OpenDatabaseAsync();
        (await AskRows.DecisionsAsync(connection, item)).Select(d => (d.Number, d.State)).ShouldBe([(1, "confirmed"), (2, "withdrawn"), (3, "confirmed")]);
        (await AskRows.EventsAsync(connection, item)).Count(e => e.Kind == "confirmed").ShouldBe(2);
        var tracked = Recorder.Events.Where(e => e.Source == "api:WorkItemDecisions.Confirm").ShouldHaveSingleItem();
        tracked.Properties[UsageProps.Count].ShouldBe(2);
    }

    [Test]
    public async Task Change_WhenOpenToAlternative_ShouldStoreAlternativeWithNote()
    {
        var item = await NewItemAsync();
        var (id, _) = await NewDecisionAsync(item, 1);

        using var response = await Api.PostJsonAsync(Url(item, "1/change"), new { kind = "alternative", alternativeIndex = 1, note = "Safer" });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
        await using var connection = await OpenDatabaseAsync();
        var decision = await AskRows.DecisionAsync(connection, id);
        (decision.State, decision.CurrentAnswer, decision.AnswerKind, decision.AlternativeIndex, decision.AnsweredByUserId)
            .ShouldBe(("changed", "Maybe", "alternative", 1, SeedIds.Users.Joel));
        var changed = (await AskRows.EventsAsync(connection, item)).Single(e => e.Kind == "changed");
        (changed.FromAnswer, changed.ToAnswer, changed.ToKind, changed.Note).ShouldBe(("Yes", "Maybe", "alternative", "Safer"));
    }

    [Test]
    public async Task Change_WhenConfirmed_ShouldPostTaggedCommentNamingFromAndTo()
    {
        var item = await NewItemAsync();
        var (id, _) = await NewDecisionAsync(item, 3, "confirmed");
        using var watch = Watch();

        using var response = await Api.PostJsonAsync(Url(item, "3/change"), new { kind = "own", text = "Not yet", note = "Legal says wait" });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
        var comment = (await Api.GetJsonAsync<List<CommentDto>>($"api/work-items/{item}/comments")).ShouldHaveSingleItem();
        comment.Body.ShouldBe("D3 changed from “Yes” to “Not yet” by Joel Dickson.\n\nLegal says wait");
        comment.Author.ShouldNotBeNull().Id.ShouldBe(SeedIds.Users.Joel);
        await using var connection = await OpenDatabaseAsync();
        var tag = (await AskRows.TagAsync(connection, comment.Id)).ShouldNotBeNull();
        (tag.Kind, tag.DecisionId, tag.QuestionId).ShouldBe(("changed", id, null));
        (await watch.NextAsync<CommentsChanged>(e => e.WorkItemId == item)).ProjectId.ShouldBe(SeedIds.Projects.Ship);
    }

    [Test]
    public async Task Change_WhenPersonOnWeb_ShouldPostCommentWithoutAgentName()
    {
        var item = await NewItemAsync();
        await NewDecisionAsync(item, 1, "confirmed");

        using var response = await Api.PostJsonAsync(Url(item, "1/change"), new { kind = "alternative", alternativeIndex = 0 });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
        var comment = (await Api.GetJsonAsync<List<CommentDto>>($"api/work-items/{item}/comments")).ShouldHaveSingleItem();
        comment.AgentName.ShouldBeNull();
        comment.Body.ShouldBe("D1 changed from “Yes” to “No” by Joel Dickson.");
    }

    [Test]
    public async Task Change_WhenOpen_ShouldNotPostComment()
    {
        var item = await NewItemAsync();
        await NewDecisionAsync(item, 1);

        using var response = await Api.PostJsonAsync(Url(item, "1/change"), new { kind = "own", text = "Something else" });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Api.GetJsonAsync<List<CommentDto>>($"api/work-items/{item}/comments")).ShouldBeEmpty();
    }

    [Test]
    public async Task Change_WhenWithdrawn_ShouldBeConflict()
    {
        var item = await NewItemAsync();
        var (id, _) = await NewDecisionAsync(item, 1, "withdrawn");

        using var response = await Api.PostJsonAsync(Url(item, "1/change"), new { kind = "own", text = "Back" });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Conflict)).Code.ShouldBe(ApiErrorCodes.Conflict);
        await using var connection = await OpenDatabaseAsync();
        (await AskRows.DecisionAsync(connection, id)).State.ShouldBe("withdrawn");
    }

    [Test]
    public async Task Change_WhenOwnTextBlank_ShouldFailValidationOnText()
    {
        var item = await NewItemAsync();
        var (id, _) = await NewDecisionAsync(item, 1);

        using var response = await Api.PostJsonAsync(Url(item, "1/change"), new { kind = "own", text = "   " });

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        (error.Code, error.Field).ShouldBe((ApiErrorCodes.Validation, "text"));
        await using var connection = await OpenDatabaseAsync();
        (await AskRows.DecisionAsync(connection, id)).State.ShouldBe("open");
    }

    [Test]
    public async Task Change_WhenAlternativeIndexOutOfRange_ShouldFailValidationOnAlternativeIndex()
    {
        var item = await NewItemAsync();
        await NewDecisionAsync(item, 1);

        using var response = await Api.PostJsonAsync(Url(item, "1/change"), new { kind = "alternative", alternativeIndex = 5 });

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        (error.Code, error.Field).ShouldBe((ApiErrorCodes.Validation, "alternativeIndex"));
    }

    [Test]
    public async Task Change_WhenBackToRecommended_ShouldConfirmAndRecordChange()
    {
        var item = await NewItemAsync();
        var (id, _) = await new DecisionBuilder(item).Number(6).Recommended("Yes").Alternatives("No", "Maybe")
            .State("changed").Answer("alternative", "No", 0).AnsweredBy(SeedIds.Users.Mei, Earlier)
            .BuildAsync(await OpenDatabaseAsync());

        using var response = await Api.PostJsonAsync(Url(item, "6/change"), new { kind = "recommended" });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
        await using var connection = await OpenDatabaseAsync();
        var decision = await AskRows.DecisionAsync(connection, id);
        (decision.State, decision.CurrentAnswer, decision.AnswerKind, decision.AlternativeIndex).ShouldBe(("confirmed", "Yes", "recommended", null));
        var changed = (await AskRows.EventsAsync(connection, item)).Single(e => e.Kind == "changed");
        (changed.FromAnswer, changed.ToAnswer, changed.ToKind).ShouldBe(("No", "Yes", "recommended"));
        (await AskRows.TagsAsync(connection, item)).ShouldHaveSingleItem().Kind.ShouldBe("changed");
    }

    [Test]
    public async Task Withdraw_WhenConfirmed_ShouldWithdrawAndKeepRow()
    {
        var item = await NewItemAsync();
        var (id, _) = await NewDecisionAsync(item, 1, "confirmed");

        using var response = await Api.PostJsonAsync(Url(item, "1/withdraw"));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
        await using var connection = await OpenDatabaseAsync();
        var decision = await AskRows.DecisionAsync(connection, id);
        (decision.State, decision.CurrentAnswer).ShouldBe(("withdrawn", "Yes"));
        var withdrawn = (await AskRows.EventsAsync(connection, item)).Single(e => e.Kind == "withdrawn");
        (withdrawn.DecisionId, withdrawn.FromAnswer, withdrawn.ByUserId).ShouldBe((id, "Yes", SeedIds.Users.Joel));
        Recorder.Events.Where(e => e.Name == UsageEventNames.DecisionWithdrawn).ShouldHaveSingleItem();
    }

    [Test]
    public async Task Discuss_WhenPosted_ShouldPostCommentTaggedAboutAndLeaveDecision()
    {
        var item = await NewItemAsync();
        var (id, _) = await NewDecisionAsync(item, 4);

        using var response = await Api.PostJsonAsync(Url(item, "4/discuss"), new { body = "> Yes\n\nAre we sure?" });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
        var comment = (await Api.GetJsonAsync<List<CommentDto>>($"api/work-items/{item}/comments")).ShouldHaveSingleItem();
        comment.Body.ShouldBe("> Yes\n\nAre we sure?");
        await using var connection = await OpenDatabaseAsync();
        var tag = (await AskRows.TagAsync(connection, comment.Id)).ShouldNotBeNull();
        (tag.Kind, tag.DecisionId).ShouldBe(("about", id));
        (await AskRows.DecisionAsync(connection, id)).State.ShouldBe("open");
        (await AskRows.EventsAsync(connection, item)).ShouldBeEmpty();
    }

    [Test]
    public async Task Discuss_WhenBodyBlank_ShouldFailValidationOnBody()
    {
        var item = await NewItemAsync();
        await NewDecisionAsync(item, 1);

        using var response = await Api.PostJsonAsync(Url(item, "1/discuss"), new { body = " " });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Field.ShouldBe("body");
    }

    [Test]
    public async Task Answer_WhenUnknownNumber_ShouldBeNotFound()
    {
        var item = await NewItemAsync();
        await NewDecisionAsync(item, 1);

        using var response = await Api.PostJsonAsync(Url(item, "confirm"), new { numbers = new[] { 9 } });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Answer_WhenNotMember_ShouldBeNotFound()
    {
        var item = await NewItemAsync();
        var (id, _) = await NewDecisionAsync(item, 1);

        using var response = await Api.ActingAs(TestUser.Kai).PostJsonAsync(Url(item, "confirm"), new { numbers = new[] { 1 } });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
        await using var connection = await OpenDatabaseAsync();
        (await AskRows.DecisionAsync(connection, id)).State.ShouldBe("open");
    }

    [Test]
    public async Task Answer_WhenDone_ShouldPublishAsksChanged()
    {
        var item = await NewItemAsync();
        await NewDecisionAsync(item, 1);
        using var watch = Watch();

        using var response = await Api.PostJsonAsync(Url(item, "confirm"), new { numbers = new[] { 1 } });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await watch.NextAsync<AsksChanged>(e => e.WorkItemId == item)).ProjectId.ShouldBe(SeedIds.Projects.Ship);
    }

    [Test]
    public async Task History_WhenChangedTwice_ShouldKeepEveryEvent()
    {
        var item = await NewItemAsync();
        await NewDecisionAsync(item, 1);

        (await Api.PostJsonAsync(Url(item, "1/change"), new { kind = "alternative", alternativeIndex = 0 })).EnsureSuccessStatusCode();
        (await Api.PostJsonAsync(Url(item, "1/change"), new { kind = "own", text = "Third" })).EnsureSuccessStatusCode();

        await using var connection = await OpenDatabaseAsync();
        (await AskRows.EventsAsync(connection, item)).Select(e => (e.Kind, e.FromAnswer, e.ToAnswer))
            .ShouldBe([("changed", "Yes", "No"), ("changed", "No", "Third")]);
    }

    [Test]
    public async Task Usage_WhenConfirmedOnWeb_ShouldTrackOnceWithCount()
    {
        var item = await NewItemAsync();
        await NewDecisionAsync(item, 1);
        Recorder.Clear();

        (await Api.PostJsonAsync(Url(item, "confirm"), new { numbers = new[] { 1 } })).EnsureSuccessStatusCode();

        var tracked = Recorder.Events.Where(e => e.Name == UsageEventNames.DecisionConfirmed).ShouldHaveSingleItem();
        (tracked.Source, tracked.Properties[UsageProps.Count]).ShouldBe(("api:WorkItemDecisions.Confirm", 1));
    }

    private static readonly DateTimeOffset Earlier = new(2026, 11, 9, 0, 0, 0, TimeSpan.Zero);

    private static string Url(Guid item, string tail) => $"api/work-items/{item}/decisions/{tail}";

    private ProjectEventWatch Watch() => new(Host.Services.GetRequiredService<IProjectEvents>(), SeedIds.Projects.Ship);

    private async Task<Guid> NewItemAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        return await new WorkItemBuilder(SeedIds.Projects.Ship).InsertAsync(connection);
    }

    // Recommended "Yes", alternatives "No" and "Maybe". A confirmed decision keeps the recommended answer.
    private async Task<(Guid Id, int Number)> NewDecisionAsync(Guid item, int number, string state = "open")
    {
        await using var connection = await OpenDatabaseAsync();
        var builder = new DecisionBuilder(item).Number(number).Recommended("Yes").Alternatives("No", "Maybe").State(state);
        if (state == "confirmed")
        {
            builder.AnsweredBy(SeedIds.Users.Joel, Earlier);
        }

        return await builder.BuildAsync(connection);
    }
}
