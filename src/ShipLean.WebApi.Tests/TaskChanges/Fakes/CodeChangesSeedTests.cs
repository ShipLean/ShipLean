using Dapper;
using ShipLean.Contracts.TaskChanges;
using ShipLean.LocalEnvironment.Database;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.TaskChanges;

namespace ShipLean.WebApi.Tests.TaskChanges.Fakes;

[TestFixture]
public class CodeChangesSeedTests
{
    private FreshDatabase _database = null!;

    [OneTimeSetUp]
    public async Task SetUp() => _database = await FreshDatabase.CreateAsync();

    [OneTimeTearDown]
    public async Task TearDown() => await _database.DisposeAsync();

    [Test]
    public async Task CodeChangesSeed_WhenSeeded_ShouldBeReadableBySql()
    {
        await using var connection = await _database.OpenAsync();
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("task").InsertAsync(connection);
        var seed = new CodeChangesSeed(connection);

        await seed.CommitAsync(item, FakeCodeHost.StorefrontRepo, FakeCodeHost.Shas.First);
        var second = await seed.CommitAsync(item, FakeCodeHost.StorefrontRepo, FakeCodeHost.Shas.Second[..7], CommitSource.ReportTask, addedByAgent: "claude");
        var comment = await seed.CommentAsync(item, FakeCodeHost.StorefrontRepo, FakeCodeHost.Shas.Second, "src/basket.ts", CodeSide.New, 2, "Why 2?", SeedIds.Users.Joel, lineText: "export const tax = 2;");
        var reply = await seed.ReplyAsync(item, comment, "Rounding", authorAgent: "claude");

        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM code_repository WHERE path = 'acme/storefront'")).ShouldBe(1);
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM code_commit WHERE subject = 'Rename cart to basket' AND files_changed = 2")).ShouldBe(1);
        (await connection.ExecuteScalarAsync<string>("SELECT sha FROM work_item_commit WHERE id = @second", new { second })).ShouldBe(FakeCodeHost.Shas.Second);
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM work_item_commit WHERE work_item_id = @item AND archived_at IS NULL", new { item })).ShouldBe(2);
        (await connection.ExecuteScalarAsync<int>("SELECT position FROM work_item_commit WHERE id = @second", new { second })).ShouldBe(2);
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM code_comment WHERE id = @comment AND parent_id IS NULL AND line = 2 AND side = 'new'", new { comment })).ShouldBe(1);
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM code_comment WHERE id = @reply AND parent_id = @comment", new { reply, comment })).ShouldBe(1);
    }
}
