using System.Text.Json;
using ShipLean.Contracts.Boards;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Taskboard;

// A class of its own: the tests change the Taskboard's card fields.
[TestFixture]
public class TaskboardEmptyExtrasApiTests : ApiFixture
{
    private async Task<TaskboardViewDto> ViewWithBugExtrasAsync(bool showEmptyFields)
    {
        await using var connection = await OpenDatabaseAsync();
        var board = await TaskboardArrangement.InsertAsync(connection, $"Empty extras {showEmptyFields}");
        var settings = await Api.GetAsync<BoardSettingsDto>($"api/boards/{SeedIds.Boards.ShipTaskboard}/settings");
        var edited = settings.Settings with
        {
            CardFields = [.. settings.Settings.CardFields.Select(f => f.Type == WorkItemTypeId.Bug
                ? f with { ShowEmptyFields = showEmptyFields, ExtraFields = [FieldIds.Severity] }
                : f)],
        };
        using var put = await Api.PutJsonAsync($"api/boards/{SeedIds.Boards.ShipTaskboard}/settings", JsonSerializer.SerializeToNode(new BoardSettingsPut(settings.Settings, edited), PersistedJson.Options));
        put.EnsureSuccessStatusCode();
        return await Api.GetAsync<TaskboardViewDto>($"api/sprints/{board.SprintId}/taskboard");
    }

    [Test]
    public async Task Taskboard_WhenShowEmptyFieldsOff_ShouldOmitAnEmptyExtraField()
    {
        var view = await ViewWithBugExtrasAsync(false);

        view.Rows.Single(r => r.Parent?.Type.Id == WorkItemTypeId.Bug).Parent!.ExtraFields.ShouldBeEmpty();
    }

    [Test]
    public async Task Taskboard_WhenShowEmptyFieldsOn_ShouldKeepAnEmptyExtraField()
    {
        var view = await ViewWithBugExtrasAsync(true);

        view.Rows.Single(r => r.Parent?.Type.Id == WorkItemTypeId.Bug).Parent!.ExtraFields
            .ShouldBe([new CardFieldDto(FieldIds.Severity, "Severity", null)]);
    }
}
