using Dapper;
using Npgsql;
using ShipLean.Contracts.Prompts;

namespace ShipLean.TestSupport.Prompts;

// Arranges a prompt_serving row by direct insert. The version, when given, must be one of the same project and prompt (the table's composite key).
public sealed class PromptServingBuilder(Guid projectId, PromptKey key)
{
    private Guid? _versionId;
    private int? _version;
    private string _builtInHash = "hash";
    private string _agent = PromptAgents.Other;
    private string? _clientName;
    private string? _clientSource;
    private string? _itemKey;
    private Guid? _userId;
    private DateTimeOffset _servedAt = TimeProvider.System.GetUtcNow();

    public PromptServingBuilder WithVersion(Guid versionId, int number)
    {
        _versionId = versionId;
        _version = number;
        return this;
    }

    public PromptServingBuilder WithBuiltInHash(string hash)
    {
        _builtInHash = hash;
        return this;
    }

    public PromptServingBuilder WithAgent(string agent)
    {
        _agent = agent;
        return this;
    }

    public PromptServingBuilder WithClient(string? name, string? source)
    {
        _clientName = name;
        _clientSource = source;
        return this;
    }

    public PromptServingBuilder WithItemKey(string? itemKey)
    {
        _itemKey = itemKey;
        return this;
    }

    public PromptServingBuilder WithUser(Guid? userId)
    {
        _userId = userId;
        return this;
    }

    public PromptServingBuilder WithServedAt(DateTimeOffset servedAt)
    {
        _servedAt = servedAt;
        return this;
    }

    public async Task<Guid> InsertAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        var id = Guid.NewGuid();
        await connection.ExecuteAsync(
            """
            INSERT INTO prompt_serving (id, project_id, prompt, version, version_id, built_in_hash, agent, client_name, client_source, item_key, user_id, served_at)
            VALUES (@id, @projectId, @wire, @version, @versionId, @builtInHash, @agent, @clientName, @clientSource, @itemKey, @userId, @servedAt)
            """,
            new
            {
                id, projectId, wire = PromptKeys.ToWire(key), version = _version, versionId = _versionId, builtInHash = _builtInHash, agent = _agent,
                clientName = _clientName, clientSource = _clientSource, itemKey = _itemKey, userId = _userId, servedAt = _servedAt.UtcDateTime,
            });
        return id;
    }
}
