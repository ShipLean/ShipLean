using Dapper;
using Npgsql;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;

namespace ShipLean.WebApi.Tests.Screens;

// Arranges who owns what and which asks are open, for the tests of what waits on a person (the counts, the backlog rows, the work item).
public static class NeedsYouArrangement
{
    // A fresh member of SHIP, so no other test's items and no seeded ask reaches them.
    public static async Task<Guid> MemberAsync(NpgsqlConnection connection, string project = "SHIP")
    {
        var user = await new AppUserBuilder().InsertAsync(connection);
        await connection.ExecuteAsync(
            "INSERT INTO project_member (id, project_id, user_id) SELECT @id, p.id, @user FROM project p WHERE p.key = @project",
            new { id = Guid.NewGuid(), user, project });
        return user;
    }

    public static async Task LeaveAsync(NpgsqlConnection connection, Guid user) =>
        await connection.ExecuteAsync("UPDATE project_member SET archived_at = now() WHERE user_id = @user", new { user });

    public static async Task<Guid> ItemAsync(NpgsqlConnection connection, string type, Guid? owner, Guid? parent = null, string? state = null)
    {
        var builder = new WorkItemBuilder(SeedIds.Projects.Ship).WithType(type).WithTitle($"Needs you {type}");
        if (owner is { } id)
        {
            builder.WithAssignee(id);
        }
        if (parent is { } p)
        {
            builder.WithParent(p);
        }
        if (state is not null)
        {
            builder.WithStateNamed(state);
        }
        return await builder.InsertAsync(connection);
    }

    public static async Task AsksAsync(NpgsqlConnection connection, Guid item, int questions, int decisions)
    {
        for (var i = 0; i < questions; i++)
        {
            await new QuestionBuilder(item).BuildAsync(connection);
        }
        for (var i = 0; i < decisions; i++)
        {
            await new DecisionBuilder(item).BuildAsync(connection);
        }
    }
}
