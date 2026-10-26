using Npgsql;

namespace SearchEmbeddings.Lexical;

// A scratch database on the SHIPLEAN_TEST_DB server: created on construction, dropped on dispose.
public sealed class LexicalDatabase : IDisposable
{
    public const string Name = "search_spike_a3";

    private readonly NpgsqlConnectionStringBuilder _admin;

    public LexicalDatabase()
    {
        var raw = Environment.GetEnvironmentVariable("SHIPLEAN_TEST_DB")
            ?? throw new InvalidOperationException("SHIPLEAN_TEST_DB is not set (source ~/.local/shiplean-env.sh)");
        _admin = new NpgsqlConnectionStringBuilder(raw) { Database = "postgres", Pooling = false };
        Drop();
        using var connection = new NpgsqlConnection(_admin.ConnectionString);
        connection.Open();
        using var create = new NpgsqlCommand($"CREATE DATABASE {Name}", connection);
        create.ExecuteNonQuery();
        Connect().Dispose();
    }

    public NpgsqlConnection Open()
    {
        var connection = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(_admin.ConnectionString) { Database = Name }.ConnectionString);
        connection.Open();
        return connection;
    }

    public void Dispose() => Drop();

    private NpgsqlConnection Connect() => Open();

    private void Drop()
    {
        using var connection = new NpgsqlConnection(_admin.ConnectionString);
        connection.Open();
        using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {Name} WITH (FORCE)", connection);
        drop.ExecuteNonQuery();
    }
}
