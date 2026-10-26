using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using ShipLean.Contracts.Boards;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Views;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Taskboard.SettingsStyles;

// The PUTs the Taskboard Styles and Tag colours panes send: the Taskboard's own board, style rules and tag colours only.
[TestFixture]
public class TaskboardStylesApiTests : ApiFixture
{
    private async Task<BoardSettingsDto> GetAsync()
        => await Api.GetAsync<BoardSettingsDto>($"api/boards/{SeedIds.Boards.ShipTaskboard}/settings");

    private Task<HttpResponseMessage> PutAsync(BoardSettingsJson basedOn, BoardSettingsJson edited)
        => Api.PutJsonAsync($"api/boards/{SeedIds.Boards.ShipTaskboard}/settings", JsonSerializer.SerializeToNode(new BoardSettingsPut(basedOn, edited), PersistedJson.Options));

    private static StyleRuleJson Rule(string id, CriteriaOp op, object value)
        => new(id, id, true, Swatch.Danger, true, false, false,
            new CriteriaJson(CriteriaMatch.All, [new CriteriaClause(FieldIds.Priority, op, JsonSerializer.SerializeToElement(value))]));

    [Test]
    public async Task TaskboardStyles_WhenRulePut_ShouldPersistInOrderAndShowOnTheTaskboardView()
    {
        var board = await GetAsync();
        var edited = board.Settings with { StyleRules = [Rule("urgent", CriteriaOp.Equal, 1), Rule("high", CriteriaOp.Equal, 2)] };

        using var response = await PutAsync(board.Settings, edited);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var saved = (await GetAsync()).Settings.StyleRules;
        saved.Select(r => r.Id).ShouldBe(["urgent", "high"]);
        saved[0].Colour.ShouldBe(Swatch.Danger);
        saved[0].Bold.ShouldBeTrue();
        var view = await Api.GetAsync<TaskboardViewDto>($"api/sprints/{SeedIds.Sprints.Sprint24}/taskboard");
        view.StyleRules.Select(r => r.Id).ShouldBe(["urgent", "high"]);
    }

    [Test]
    public async Task TaskboardStyles_WhenCriteriaInvalid_ShouldBe400AndKeepTheSaved()
    {
        var board = await GetAsync();
        var edited = board.Settings with { StyleRules = [Rule("bad", CriteriaOp.Contains, "1")] };

        using var response = await PutAsync(board.Settings, edited);

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        (await GetAsync()).Settings.StyleRules.Select(r => r.Id).ShouldBe(board.Settings.StyleRules.Select(r => r.Id));
    }

    [Test]
    public async Task TaskboardStyles_WhenTagColourPut_ShouldPersistAndLeaveTheRulesAlone()
    {
        var board = await GetAsync();
        var edited = board.Settings with { TagColours = [new TagColourJson("Blocked", Swatch.Warn)] };

        using var response = await PutAsync(board.Settings, edited);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var saved = (await GetAsync()).Settings;
        saved.TagColours.ShouldBe([new TagColourJson("Blocked", Swatch.Warn)]);
        saved.StyleRules.Select(r => r.Id).ShouldBe(board.Settings.StyleRules.Select(r => r.Id));
    }
}
