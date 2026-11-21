using Dapper;
using Npgsql;
using ShipLean.Application.Features.Prompts;
using ShipLean.Contracts.Prompts;
using ShipLean.WebApi.Features.Prompts;

namespace ShipLean.TestSupport.Prompts;

// Arranges a project's custom prompt version by direct insert: the base built-in row (the given text, else the current built-in), the version,
// and optionally the project_prompt row that puts it in use or records the built-in change the project kept. Nothing in production uses it.
public sealed class PromptVersionBuilder(Guid projectId, PromptKey key)
{
    private int? _number;
    private string _text = "Custom text.";
    private string _note = "A change.";
    private PromptVersionSource _source = PromptVersionSource.Editor;
    private Guid? _createdBy;
    private DateTimeOffset _createdAt = TimeProvider.System.GetUtcNow();
    private string? _builtInText;
    private bool _inUse;
    private string? _keptBuiltInHash;

    public PromptVersionBuilder WithNumber(int number)
    {
        _number = number;
        return this;
    }

    public PromptVersionBuilder WithText(string text)
    {
        _text = text;
        return this;
    }

    public PromptVersionBuilder WithNote(string note)
    {
        _note = note;
        return this;
    }

    public PromptVersionBuilder WithSource(PromptVersionSource source)
    {
        _source = source;
        return this;
    }

    public PromptVersionBuilder WithCreatedBy(Guid? userId)
    {
        _createdBy = userId;
        return this;
    }

    public PromptVersionBuilder WithCreatedAt(DateTimeOffset createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    // The built-in this version started from; the default is the current built-in.
    public PromptVersionBuilder WithBuiltInText(string text)
    {
        _builtInText = text;
        return this;
    }

    public PromptVersionBuilder InUse()
    {
        _inUse = true;
        return this;
    }

    // The built-in hash the project chose to keep its own text over.
    public PromptVersionBuilder WithKeptBuiltInHash(string hash)
    {
        _keptBuiltInHash = hash;
        return this;
    }

    public async Task<Guid> InsertAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        var wire = PromptKeys.ToWire(key);
        var builtIn = (_builtInText ?? new BuiltInPrompts().Text(key)).Replace("\r\n", "\n", StringComparison.Ordinal);
        var hash = PromptHash.Of(builtIn);
        var id = Guid.NewGuid();
        await connection.ExecuteAsync(
            """
            INSERT INTO prompt_built_in (prompt, hash, text, first_seen_at) VALUES (@wire, @hash, @builtIn, @createdAt)
            ON CONFLICT DO NOTHING
            """,
            new { wire, hash, builtIn, createdAt = _createdAt.UtcDateTime });
        await connection.ExecuteAsync(
            """
            INSERT INTO prompt_version (id, project_id, prompt, number, text, base_hash, note, source, created_by, created_at)
            VALUES (@id, @projectId, @wire, COALESCE(@number, (SELECT COALESCE(MAX(number), 0) + 1 FROM prompt_version WHERE project_id = @projectId AND prompt = @wire)),
                    @text, @hash, @note, @source, @createdBy, @createdAt)
            """,
            new
            {
                id, projectId, wire, number = _number, text = _text, hash, note = _note,
                source = char.ToLowerInvariant(_source.ToString()[0]) + _source.ToString()[1..], createdBy = _createdBy, createdAt = _createdAt.UtcDateTime,
            });
        if (_inUse || _keptBuiltInHash is not null)
        {
            await connection.ExecuteAsync(
                """
                INSERT INTO project_prompt (project_id, prompt, in_use_version_id, kept_built_in_hash, updated_at)
                VALUES (@projectId, @wire, @inUse, @kept, @createdAt)
                ON CONFLICT (project_id, prompt) DO UPDATE
                SET in_use_version_id = COALESCE(excluded.in_use_version_id, project_prompt.in_use_version_id),
                    kept_built_in_hash = COALESCE(excluded.kept_built_in_hash, project_prompt.kept_built_in_hash),
                    updated_at = excluded.updated_at
                """,
                new { projectId, wire, inUse = _inUse ? id : (Guid?)null, kept = _keptBuiltInHash, createdAt = _createdAt.UtcDateTime });
        }
        return id;
    }
}
