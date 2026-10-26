using System.Text.Json;
using Npgsql;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.Sprints.End;

// Arranges the seeded Sprint 24 as an ended sprint (its dates move to the past, relative to the database's UTC today, so
// no clock is touched) and reads what the rollover endpoint says about it.
internal static class EndedSprint
{
    public static async Task EndSprint24Async(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            UPDATE sprint
            SET start_date = (now() AT TIME ZONE 'utc')::date - 14, end_date = (now() AT TIME ZONE 'utc')::date - 1
            WHERE id = @id
            """,
            connection);
        command.Parameters.AddWithValue("id", SeedIds.Sprints.Sprint24);
        (await command.ExecuteNonQueryAsync()).ShouldBe(1);
    }

    // Leaves the one capacity row at `points` and the rest of the sprint's capacity at zero.
    public static async Task SetTeamCapacityAsync(string connectionString, Guid sprintId, Guid keepRow, decimal points)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "UPDATE sprint_capacity SET capacity_points = CASE WHEN id = @keep THEN @points ELSE 0 END WHERE sprint_id = @sprint AND archived_at IS NULL",
            connection);
        command.Parameters.AddWithValue("keep", keepRow);
        command.Parameters.AddWithValue("points", points);
        command.Parameters.AddWithValue("sprint", sprintId);
        (await command.ExecuteNonQueryAsync()).ShouldBeGreaterThan(0);
    }

    public static async Task<JsonElement> ReadRolloverAsync(IPage page, Guid sprintId)
    {
        var response = await page.Context.APIRequest.GetAsync($"/api/sprints/{sprintId}/rollover");
        response.Ok.ShouldBeTrue(await response.TextAsync());
        return JsonDocument.Parse(await response.TextAsync()).RootElement.Clone();
    }

    public static IReadOnlyList<string> Keys(JsonElement rollover) =>
        [.. rollover.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("key").GetString()!)];

    public static decimal Points(JsonElement rollover) =>
        rollover.GetProperty("items").EnumerateArray().Sum(i => i.GetProperty("points").ValueKind == JsonValueKind.Null ? 0 : i.GetProperty("points").GetDecimal());
}
