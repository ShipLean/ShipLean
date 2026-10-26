using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using ShipLean.Contracts.Boards;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Taskboard.Settings;

// The PUT the Taskboard Fields pane sends: the Taskboard's own board, card fields only.
[TestFixture]
public class TaskboardFieldsApiTests : ApiFixture
{
    private async Task<BoardSettingsDto> GetAsync()
        => await Api.GetAsync<BoardSettingsDto>($"api/boards/{SeedIds.Boards.ShipTaskboard}/settings");

    private Task<HttpResponseMessage> PutAsync(BoardSettingsJson basedOn, BoardSettingsJson edited)
        => Api.PutJsonAsync($"api/boards/{SeedIds.Boards.ShipTaskboard}/settings", JsonSerializer.SerializeToNode(new BoardSettingsPut(basedOn, edited), PersistedJson.Options));

    [Test]
    public async Task TaskboardFields_WhenPutWithAFieldAdded_ShouldPersistAndShowOnTheTaskboardView()
    {
        var board = await GetAsync();
        board.Level.ShouldBe(BoardLevel.Taskboard);
        var task = board.Settings.CardFields.Single(f => f.Type == WorkItemTypeId.Task);
        var edited = board.Settings with
        {
            CardFields = [.. board.Settings.CardFields.Select(f => f.Type == WorkItemTypeId.Task
                ? f with { ShowPoints = true, ExtraFields = [.. task.ExtraFields, FieldIds.Priority] }
                : f)],
        };

        using var response = await PutAsync(board.Settings, edited);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var saved = (await GetAsync()).Settings.CardFields.Single(f => f.Type == WorkItemTypeId.Task);
        saved.ShowPoints.ShouldBeTrue();
        saved.ExtraFields.ShouldBe([.. task.ExtraFields, FieldIds.Priority]);
        var view = await Api.GetAsync<TaskboardViewDto>($"api/sprints/{SeedIds.Sprints.Sprint24}/taskboard");
        view.CardFields.Single(f => f.Type == WorkItemTypeId.Task).ExtraFields.ShouldBe(saved.ExtraFields);
    }

    [Test]
    public async Task TaskboardFields_WhenElevenExtraFields_ShouldBe400AndKeepTheSaved()
    {
        var board = await GetAsync();
        var eleven = Enumerable.Range(0, 11).Select(i => $"extra{i}").ToList();
        var edited = board.Settings with
        {
            CardFields = [.. board.Settings.CardFields.Select(f => f.Type == WorkItemTypeId.Task ? f with { ExtraFields = eleven } : f)],
        };

        using var response = await PutAsync(board.Settings, edited);

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Error.ShouldContain("at most 10 extra fields");
        (await GetAsync()).Settings.CardFields.Single(f => f.Type == WorkItemTypeId.Task).ExtraFields.ShouldBe(board.Settings.CardFields.Single(f => f.Type == WorkItemTypeId.Task).ExtraFields);
    }
}
