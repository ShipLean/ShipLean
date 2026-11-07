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

    // Adds a sprint with nothing in it to the project and returns its id.
    public static async Task<Guid> InsertEmptySprintAsync(string connectionString, string projectKey, string name, DateOnly start, DateOnly end)
    {
        var id = Guid.NewGuid();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "INSERT INTO sprint (id, project_id, name, start_date, end_date) SELECT @id, p.id, @name, @start, @end FROM project p WHERE p.key = @key", connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("key", projectKey);
        command.Parameters.AddWithValue("name", name);
        command.Parameters.AddWithValue("start", start);
        command.Parameters.AddWithValue("end", end);
        (await command.ExecuteNonQueryAsync()).ShouldBe(1);
        return id;
    }

    public static async Task<bool> IsArchivedAsync(string connectionString, Guid sprintId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT archived_at IS NOT NULL FROM sprint WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", sprintId);
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    // Clears every entered capacity (soft: archived), so no sprint has a team total and all plan against velocity.
    public static async Task ArchiveAllCapacityAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("UPDATE sprint_capacity SET archived_at = now() WHERE archived_at IS NULL", connection);
        (await command.ExecuteNonQueryAsync()).ShouldBeGreaterThan(0);
    }

    // Switches a project to the Year, quarter and number convention (new projects start there; the seeded ones do not).
    public static async Task UseYearQuarterNamingAsync(string connectionString, string projectKey)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("UPDATE project SET sprint_naming = 'yearQuarterNumber' WHERE key = @key", connection);
        command.Parameters.AddWithValue("key", projectKey);
        (await command.ExecuteNonQueryAsync()).ShouldBe(1);
    }
}
