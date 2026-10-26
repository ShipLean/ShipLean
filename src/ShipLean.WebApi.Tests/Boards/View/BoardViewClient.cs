using System.Net;
using System.Text.Json;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Views;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Boards.View;

// Reads a level board over the API the way the SPA does.
internal static class BoardViewClient
{
    public static async Task<BoardViewDto> BoardAsync(this ApiClient api, Guid boardId, string query = "")
    {
        using var response = await api.GetAsync($"api/boards/{boardId}{query}");
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonSerializer.Deserialize<BoardViewDto>(text, PersistedJson.Options)!;
    }

    public static IEnumerable<CardDto> Cards(this BoardViewDto board) => board.Lanes.SelectMany(l => l.Cells).SelectMany(c => c.Cards);

    public static BoardLaneDto Lane(this BoardViewDto board, string laneId) => board.Lanes.Single(l => l.Id == laneId);

    public static BoardCellDto Cell(this BoardLaneDto lane, string columnId, bool doneHalf = false) => lane.Cells.Single(c => c.ColumnId == columnId && c.DoneHalf == doneHalf);

    public static async Task SetLanePreferenceAsync(this ApiClient api, Guid boardId, string value)
    {
        using var response = await api.PutJsonAsync($"api/me/preferences/view:board-lanes:{boardId}", value);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
    }
}
