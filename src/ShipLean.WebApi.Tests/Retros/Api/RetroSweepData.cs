using Dapper;
using Npgsql;
using ShipLean.TestSupport;

namespace ShipLean.WebApi.Tests.Retros.Api;

// A minimal theme, retro and point in SHIP, by raw SQL, for the routes keyed by a point or theme id: a made-up id is a 404 from the
// access check, which would hide whether the check runs. Fixtures that call those routes insert it once in their setup.
internal static class RetroSweepData
{
    public static readonly Guid Theme = new("0a2e0000-0000-7000-8000-000000000001");
    public static readonly Guid Retro = new("0a2e0000-0000-7000-8000-000000000002");
    public static readonly Guid Point = new("0a2e0000-0000-7000-8000-000000000003");

    public static async Task InsertAsync(NpgsqlConnection connection)
    {
        await connection.ExecuteAsync(
            """
            INSERT INTO retro_theme (id, project_id, name, name_set_by, tone, point_count) VALUES (@Theme, @project, 'Sweep theme', 'agent', 'bad', 1);
            INSERT INTO retrospective (id, project_id, work_item_id, author_user_id, agent_name, role)
            VALUES (@Retro, @project, @item, @user, 'Sweep agent', 'subAgent');
            INSERT INTO retro_point (id, retrospective_id, category, text, theme_id, theme_set_by, created_by_user_id, agent_name)
            VALUES (@Point, @Retro, 'improve', 'Sweep point', @Theme, 'agent', @user, 'Sweep agent');
            """,
            new { Theme, Retro, Point, project = SeedIds.Projects.Ship, item = SeedIds.WorkItems.Ship90, user = SeedIds.Users.Joel });
    }
}
