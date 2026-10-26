using System.Reflection;
using Dapper;
using Npgsql;
using ShipLean.Application.Database;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Database;

namespace ShipLean.WebApi.Tests.WorkItemDetails.Attachments;

// The live-name migration against data from before it: its own database, the index dropped, duplicates inserted, the script run again.
[TestFixture]
public class AttachmentLiveNameMigrationTests
{
    private const string Script = "202610251900_attachments_live_name_unique.sql";

    [Test]
    public async Task Migration_WhenLiveNamesAreDuplicated_ShouldArchiveAllButTheNewest()
    {
        await using var database = await ShipLeanDatabase.CloneSeedAsync();
        await using var connection = await database.OpenAsync();
        await connection.ExecuteAsync("DROP INDEX ux_attachment_live_name");
        var item = SeedIds.WorkItems.Ship91;
        var older = await InsertAsync(connection, item, "dup.txt", "3 days");
        var newest = await InsertAsync(connection, item, "dup.txt", "1 day");
        var middle = await InsertAsync(connection, item, "dup.txt", "2 days");
        var other = await InsertAsync(connection, item, "other.txt", "1 day");

        await connection.ExecuteAsync(ReadScript());

        var live = (await connection.QueryAsync<Guid>("SELECT id FROM attachment WHERE work_item_id = @item AND archived_at IS NULL", new { item })).ToList();
        live.Order().ShouldBe(new[] { newest, other }.Order());
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM attachment WHERE id = ANY(@ids)", new { ids = new[] { older, middle } })).ShouldBe(2);
        var duplicate = await Should.ThrowAsync<PostgresException>(() => InsertAsync(connection, item, "other.txt", "0 days"));
        duplicate.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
    }

    private static Task<Guid> InsertAsync(NpgsqlConnection connection, Guid item, string name, string age)
        => connection.ExecuteScalarAsync<Guid>(
            """
            INSERT INTO attachment (id, work_item_id, name, content_type, size_bytes, storage_ref, created_at)
            VALUES (gen_random_uuid(), @item, @name, 'text/plain', 1, 'x', now() - @age::interval) RETURNING id
            """,
            new { item, name, age });

    private static string ReadScript()
    {
        var assembly = typeof(DatabaseScripts).Assembly;
        var resource = assembly.GetManifestResourceNames().Single(n => n.EndsWith(Script, StringComparison.Ordinal));
        using var reader = new StreamReader(assembly.GetManifestResourceStream(resource)!);
        return reader.ReadToEnd();
    }
}
