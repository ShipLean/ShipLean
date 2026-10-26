using Dapper;
using Npgsql;
using ShipLean.Application.Database;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Database;

namespace ShipLean.WebApi.Tests.Agents.Stores;

// One seeded clone per fixture class; every test gets a world of its own (its own clock and users) over it.
internal sealed class PostgresAgentDatabase : IAsyncDisposable
{
    private readonly ClonedDatabase _database;

    private PostgresAgentDatabase(ClonedDatabase database)
    {
        _database = database;
        DataSource = NpgsqlDataSource.Create(DatabaseServices.PinUtc(database.ConnectionString));
    }

    public NpgsqlDataSource DataSource { get; }

    public static async Task<PostgresAgentDatabase> CreateAsync()
    {
        DapperTypeHandlers.Register();
        return new PostgresAgentDatabase(await ShipLeanDatabase.CloneEmptyAsync());
    }

    public PostgresAgentWorld NewWorld() => new(this);

    public async Task<Guid> InsertUserAsync()
    {
        await using var connection = await _database.OpenAsync();
        return await new AppUserBuilder().InsertAsync(connection);
    }

    public async Task ExecuteAsync(string sql, object parameters)
    {
        await using var connection = await _database.OpenAsync();
        await connection.ExecuteAsync(sql, parameters);
    }

    // The consent contract's arrange hook is synchronous, so this is too.
    public Guid InsertRequest(string clientId, string redirectUri, string? state, string codeChallenge, string[] scopes, string resource, Guid userId, DateTimeOffset expiresAt)
    {
        var id = Guid.NewGuid();
        using var connection = DataSource.OpenConnection();
        connection.Execute(
            """
            INSERT INTO oauth_request (id, client_id, redirect_uri, state, code_challenge, scopes, resource, user_id, expires_at)
            VALUES (@id, @clientId, @redirectUri, @state, @codeChallenge, @scopes, @resource, @userId, @expiresAt)
            """,
            new { id, clientId, redirectUri, state, codeChallenge, scopes, resource, userId, expiresAt });
        return id;
    }

    public async Task<T> QueryAsync<T>(string sql, object parameters)
    {
        await using var connection = await _database.OpenAsync();
        return await connection.QuerySingleAsync<T>(sql, parameters);
    }

    public async ValueTask DisposeAsync()
    {
        await DataSource.DisposeAsync();
        await _database.DisposeAsync();
    }
}
