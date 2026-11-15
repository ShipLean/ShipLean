using Dapper;
using Npgsql;
using ShipLean.Contracts.TaskChanges;
using ShipLean.TestSupport;
using ShipLean.TestSupport.TaskChanges;

namespace ShipLean.WebApi.Tests.TaskChanges.Http;

// The comment the reply and resolve sweep rows name (TaskChangesRows.Comment): a made-up id is a 404 whether or not access
// is checked, so SHIP-172 gets a commit and a top-level comment with that id.
internal static class TaskChangesSweepData
{
    public static async Task InsertAsync(NpgsqlConnection connection)
    {
        var seed = new CodeChangesSeed(connection);
        var repo = FakeCodeHost.StorefrontRepo;
        await seed.CommitAsync(SeedIds.WorkItems.Ship172, repo, FakeCodeHost.Shas.First);
        var id = await seed.CommentAsync(SeedIds.WorkItems.Ship172, repo, FakeCodeHost.Shas.First, "src/basket.ts", CodeSide.New, 3, "Sweep comment");
        await connection.ExecuteAsync(
            "UPDATE code_comment SET id = @fixedId WHERE id = @id",
            new { fixedId = Guid.Parse(TaskChangesRows.Comment), id });
    }
}
