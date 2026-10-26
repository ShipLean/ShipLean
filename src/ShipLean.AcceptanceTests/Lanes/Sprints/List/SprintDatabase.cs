using System.Globalization;
using Npgsql;

namespace ShipLean.AcceptanceTests.Lanes.Sprints.List;

// Reads and arranges sprint rows the way the tests need them. The dates come back as text, as the page shows them.
public static class SprintDatabase
{
    public sealed record Dates(DateOnly Start, DateOnly End);

    private static DateOnly Day(string text) => DateOnly.ParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static async Task<Dates> ReadDatesAsync(string connectionString, Guid sprintId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT to_char(start_date, 'YYYY-MM-DD'), to_char(end_date, 'YYYY-MM-DD') FROM sprint WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", sprintId);
        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).ShouldBeTrue();
        return new Dates(Day(reader.GetString(0)), Day(reader.GetString(1)));
    }

    public static async Task<Dates> ReadDatesByNameAsync(string connectionString, string projectKey, string name)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT to_char(s.start_date, 'YYYY-MM-DD'), to_char(s.end_date, 'YYYY-MM-DD')
            FROM sprint s JOIN project p ON p.id = s.project_id
            WHERE p.key = @key AND s.name = @name AND s.archived_at IS NULL
            """,
            connection);
        command.Parameters.AddWithValue("key", projectKey);
        command.Parameters.AddWithValue("name", name);
        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).ShouldBeTrue();
        return new Dates(Day(reader.GetString(0)), Day(reader.GetString(1)));
    }

    // Clears every entered capacity (soft: archived), so no sprint has a team total and all plan against velocity.
    public static async Task ArchiveAllCapacityAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("UPDATE sprint_capacity SET archived_at = now() WHERE archived_at IS NULL", connection);
        (await command.ExecuteNonQueryAsync()).ShouldBeGreaterThan(0);
    }
}
