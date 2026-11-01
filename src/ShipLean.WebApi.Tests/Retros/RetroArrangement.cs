using Dapper;
using Npgsql;

namespace ShipLean.WebApi.Tests.Retros;

// Small arrangements the retro helper tests share: a sprint of a project and a time at noon UTC on a day.
internal static class RetroArrangement
{
    public static async Task<Guid> SprintAsync(NpgsqlConnection connection, Guid projectId, string name, DateOnly start, DateOnly end)
    {
        var id = Guid.NewGuid();
        await connection.ExecuteAsync(
            "INSERT INTO sprint (id, project_id, name, start_date, end_date) VALUES (@id, @projectId, @name, @start, @end)",
            new { id, projectId, name, start, end });
        return id;
    }

    public static DateTimeOffset Noon(int year, int month, int day) => new(year, month, day, 12, 0, 0, TimeSpan.Zero);
}
