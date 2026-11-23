using Dapper;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Prompts;

namespace ShipLean.Application.Features.Prompts.Writes;

// C6 over C7's tables. Every call is one transaction holding the project's project_prompt row for the prompt, so two writes can't take the same
// version number. Nothing is deleted: a version stays, and "use built-in" only moves the pointer. Callers check access first (Admin).
// Every save stamps agent_name with the acting agent (null on the web); source mcp is only for an agent.
public sealed class DbPromptWrites(NpgsqlDataSource dataSource, IBuiltInPrompts builtIns, ICurrentUser currentUser, ICurrentAgent currentAgent, TimeProvider clock) : IPromptWrites
{
    private sealed record VersionRow(Guid Id, string BaseHash);

    private sealed record Pointer(Guid? InUseId, string? KeptHash);

    private sealed record InUse(int Number, string Text, string BaseHash);

    private sealed record State(Guid? InUseId, int? InUseNumber, string? InUseText, string? InUseBaseHash, string? KeptHash);

    public async Task<PromptSavedDto> SaveAsync(Guid projectId, PromptKey key, PromptSaveDto save, PromptBase? expected = null, CancellationToken cancellationToken = default)
    {
        if (save.Source == PromptVersionSource.Migrated)
        {
            throw ApiException.Validation("A version can't be saved as migrated.", "source");
        }

        var agentName = AgentStamp.Of(currentAgent);
        if (save.Source == PromptVersionSource.Mcp && agentName is null)
        {
            throw ApiException.Validation("Source mcp is only for agents.", "source");
        }

        var note = PromptTextRules.Note(save.Note);
        var text = PromptTextRules.Text(save.Text);

        var wire = PromptKeys.ToWire(key);
        var current = builtIns.Hash(key);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var state = await LockAsync(connection, transaction, projectId, wire, cancellationToken);
        // Stale before unchanged: a save racing another writer who saved the same text is reported as stale.
        PromptTextRules.Expected(expected, state.InUseNumber);
        PromptTextRules.Changed(text, state.InUseText ?? PromptTextRules.Normalised(builtIns.Text(key)));

        var baseHash = save.Source == PromptVersionSource.BuiltInUpdate ? current : state.InUseBaseHash ?? current;
        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO prompt_built_in (prompt, hash, text, first_seen_at) VALUES (@wire, @current, @builtInText, @now) ON CONFLICT DO NOTHING",
            new { wire, current, builtInText = PromptTextRules.Normalised(builtIns.Text(key)), now = clock.GetUtcNow().UtcDateTime },
            transaction, cancellationToken: cancellationToken));
        var versionId = Guid.NewGuid();
        var number = await connection.QuerySingleAsync<int>(new CommandDefinition(
            """
            INSERT INTO prompt_version (id, project_id, prompt, number, text, base_hash, note, source, agent_name, created_by, created_at)
            SELECT @versionId, @projectId, @wire, COALESCE(MAX(number), 0) + 1, @text, @baseHash, @note, @source, @agentName, @by, @now
            FROM prompt_version WHERE project_id = @projectId AND prompt = @wire
            RETURNING number
            """,
            new { versionId, projectId, wire, text, baseHash, note, source = PromptKeys.ToWire(save.Source), agentName, by = currentUser.Id, now = clock.GetUtcNow().UtcDateTime },
            transaction, cancellationToken: cancellationToken));
        await SetPointerAsync(connection, transaction, projectId, wire, versionId, clearKept: true, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new PromptSavedDto(key, StatusFor(baseHash, current), number);
    }

    public async Task<PromptSavedDto> UseBuiltInAsync(Guid projectId, PromptKey key, CancellationToken cancellationToken = default)
    {
        var wire = PromptKeys.ToWire(key);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var state = await LockAsync(connection, transaction, projectId, wire, cancellationToken);
        if (state.InUseId is null)
        {
            throw ApiException.Conflict("This prompt already uses the built-in.");
        }

        await SetPointerAsync(connection, transaction, projectId, wire, null, clearKept: false, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new PromptSavedDto(key, PromptStatus.UsingBuiltIn, null);
    }

    public async Task<PromptSavedDto> UseVersionAsync(Guid projectId, PromptKey key, int number, CancellationToken cancellationToken = default)
    {
        var wire = PromptKeys.ToWire(key);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await LockAsync(connection, transaction, projectId, wire, cancellationToken);
        var version = await connection.QuerySingleOrDefaultAsync<VersionRow>(new CommandDefinition(
            "SELECT id, base_hash AS BaseHash FROM prompt_version WHERE project_id = @projectId AND prompt = @wire AND number = @number",
            new { projectId, wire, number }, transaction, cancellationToken: cancellationToken))
            ?? throw ApiException.NotFound("There is no such version of this prompt.");
        await SetPointerAsync(connection, transaction, projectId, wire, version.Id, clearKept: false, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new PromptSavedDto(key, StatusFor(version.BaseHash, builtIns.Hash(key)), number);
    }

    public async Task<PromptSavedDto> KeepMineAsync(Guid projectId, PromptKey key, CancellationToken cancellationToken = default)
    {
        var wire = PromptKeys.ToWire(key);
        var current = builtIns.Hash(key);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var state = await LockAsync(connection, transaction, projectId, wire, cancellationToken);
        if (state.InUseId is null || state.InUseBaseHash == current || state.KeptHash == current)
        {
            throw ApiException.Conflict("There is no built-in change to keep.");
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE project_prompt SET kept_built_in_hash = @current, updated_by = @by, updated_at = @now WHERE project_id = @projectId AND prompt = @wire",
            new { current, by = currentUser.Id, now = clock.GetUtcNow().UtcDateTime, projectId, wire }, transaction, cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        return new PromptSavedDto(key, PromptStatus.Custom, state.InUseNumber);
    }

    private static PromptStatus StatusFor(string baseHash, string current) => baseHash == current ? PromptStatus.Custom : PromptStatus.BuiltInUpdated;

    // Makes sure the project's row exists, then holds it for the rest of the transaction.
    private async Task<State> LockAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, string wire, CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO project_prompt (project_id, prompt, updated_at) VALUES (@projectId, @wire, @now) ON CONFLICT DO NOTHING",
            new { projectId, wire, now = clock.GetUtcNow().UtcDateTime }, transaction, cancellationToken: cancellationToken));
        // Lock the pointer row alone, then read the version in a second statement: a join under FOR UPDATE returns nulls for the joined columns when the lock waited.
        var pointer = await connection.QuerySingleAsync<Pointer>(new CommandDefinition(
            "SELECT in_use_version_id AS InUseId, kept_built_in_hash AS KeptHash FROM project_prompt WHERE project_id = @projectId AND prompt = @wire FOR UPDATE",
            new { projectId, wire }, transaction, cancellationToken: cancellationToken));
        if (pointer.InUseId is null)
        {
            return new State(null, null, null, null, pointer.KeptHash);
        }

        var version = await connection.QuerySingleAsync<InUse>(new CommandDefinition(
            "SELECT number AS Number, text AS Text, base_hash AS BaseHash FROM prompt_version WHERE id = @id",
            new { id = pointer.InUseId }, transaction, cancellationToken: cancellationToken));
        return new State(pointer.InUseId, version.Number, version.Text, version.BaseHash, pointer.KeptHash);
    }

    private async Task SetPointerAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, string wire, Guid? versionId, bool clearKept, CancellationToken cancellationToken)
        => await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE project_prompt
            SET in_use_version_id = @versionId, kept_built_in_hash = CASE WHEN @clearKept THEN NULL ELSE kept_built_in_hash END, updated_by = @by, updated_at = @now
            WHERE project_id = @projectId AND prompt = @wire
            """,
            new { versionId, clearKept, by = currentUser.Id, now = clock.GetUtcNow().UtcDateTime, projectId, wire }, transaction, cancellationToken: cancellationToken));
}
