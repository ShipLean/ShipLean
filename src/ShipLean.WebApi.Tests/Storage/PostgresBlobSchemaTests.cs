using System.Text;
using Dapper;
using Npgsql;
using ShipLean.Application.Database;
using ShipLean.Application.Storage;
using ShipLean.TestSupport.Database;

namespace ShipLean.WebApi.Tests.Storage;

// ApplySchema on a database with no app schema, and what a put writes to the blob row.
[TestFixture]
public class PostgresBlobSchemaTests
{
    [Test]
    public async Task ApplySchema_WhenDatabaseIsEmpty_ShouldCreateTableAndBeIdempotent()
    {
        await using var database = await FreshDatabase.CreateAsync(includeSeed: false);
        var empty = await CreateEmptyDatabaseAsync(database);
        try
        {
            PostgresBlobStore.ApplySchema(DatabaseServices.PinUtc(empty));
            PostgresBlobStore.ApplySchema(DatabaseServices.PinUtc(empty));

            await using var connection = new NpgsqlConnection(empty);
            await connection.OpenAsync();
            (await connection.ExecuteScalarAsync<bool>("SELECT to_regclass('public.blob') IS NOT NULL")).ShouldBeTrue();
            (await connection.ExecuteScalarAsync<bool>("SELECT to_regclass('public.schemaversions') IS NULL")).ShouldBeTrue();
            (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM blob_schemaversions")).ShouldBe(1);
        }
        finally
        {
            await DropDatabaseAsync(database, empty);
        }
    }

    [Test]
    public async Task ApplySchema_WhenAppDatabase_ShouldKeepItsOwnJournal()
    {
        await using var database = await FreshDatabase.CreateAsync();

        PostgresBlobStore.ApplySchema(DatabaseServices.PinUtc(database.ConnectionString));
        PostgresBlobStore.ApplySchema(DatabaseServices.PinUtc(database.ConnectionString));

        await using var connection = await database.OpenAsync();
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM blob_schemaversions")).ShouldBe(1);
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM schemaversions WHERE scriptname LIKE 'db/blobs/%'")).ShouldBe(0);
    }

    [Test]
    public async Task PutAsync_ShouldWriteSizeAndContentTypeToTheRow()
    {
        await using var database = await FreshDatabase.CreateAsync(includeSeed: false);
        PostgresBlobStore.ApplySchema(DatabaseServices.PinUtc(database.ConnectionString));
        await using var store = new PostgresBlobStore(DatabaseServices.PinUtc(database.ConnectionString));

        await store.PutAsync("covers/a.png", new MemoryStream(Encoding.UTF8.GetBytes("twelve bytes")), "image/png", CancellationToken.None);

        await using var connection = await database.OpenAsync();
        var row = await connection.QuerySingleAsync<(string ContentType, long SizeBytes, int Length)>(
            "SELECT content_type, size_bytes, length(bytes) FROM blob WHERE key = 'covers/a.png'");
        row.ContentType.ShouldBe("image/png");
        row.SizeBytes.ShouldBe(12);
        row.Length.ShouldBe(12);
    }

    // A second, empty database on the same server (the connection of `like` is the server's admin).
    private static async Task<string> CreateEmptyDatabaseAsync(FreshDatabase like)
    {
        var name = $"shiplean_e_{Guid.NewGuid():N}";
        await using var connection = await like.OpenAsync();
        await connection.ExecuteAsync($"CREATE DATABASE \"{name}\" TEMPLATE template0 ENCODING 'UTF8'");
        return new NpgsqlConnectionStringBuilder(like.ConnectionString) { Database = name, Pooling = false }.ConnectionString;
    }

    private static async Task DropDatabaseAsync(FreshDatabase like, string connectionString)
    {
        var name = new NpgsqlConnectionStringBuilder(connectionString).Database;
        await using var connection = await like.OpenAsync();
        await connection.ExecuteAsync($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)");
    }
}
