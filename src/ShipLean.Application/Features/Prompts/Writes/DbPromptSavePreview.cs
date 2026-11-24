using System.Text;
using Dapper;
using Npgsql;
using ShipLean.Application.Features.Prompts.Updates;
using ShipLean.Contracts.Prompts;

namespace ShipLean.Application.Features.Prompts.Writes;

// C3: what save_prompt would write, without writing. The rules are the save's (PromptTextRules) in the save's order: note (only its
// 200-character limit; a blank note is allowed, D20), text, expected, then "Nothing changed". It reads the version in use without the lock; the save checks `expected` again under the lock.
public sealed class DbPromptSavePreview(NpgsqlDataSource dataSource, IBuiltInPrompts builtIns) : IPromptSavePreview
{
    private sealed record Current(int? InUseNumber, string? InUseText, int MaxNumber);

    public async Task<PromptSavePreviewDto> PreviewAsync(Guid projectId, PromptKey key, string text, string note, PromptBase expected, CancellationToken cancellationToken = default)
    {
        PromptTextRules.Note(note);
        var normalised = PromptTextRules.Text(text);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var current = await connection.QuerySingleAsync<Current>(new CommandDefinition(
            """
            SELECT
                (SELECT v.number FROM project_prompt p JOIN prompt_version v ON v.id = p.in_use_version_id WHERE p.project_id = @projectId AND p.prompt = @wire) AS InUseNumber,
                (SELECT v.text FROM project_prompt p JOIN prompt_version v ON v.id = p.in_use_version_id WHERE p.project_id = @projectId AND p.prompt = @wire) AS InUseText,
                (SELECT COALESCE(MAX(number), 0) FROM prompt_version WHERE project_id = @projectId AND prompt = @wire) AS MaxNumber
            """,
            new { projectId, wire = PromptKeys.ToWire(key) }, cancellationToken: cancellationToken));

        PromptTextRules.Expected(expected, current.InUseNumber);
        var inUse = current.InUseText ?? PromptTextRules.Normalised(builtIns.Text(key));
        PromptTextRules.Changed(normalised, inUse);

        return new PromptSavePreviewDto(
            key, current.InUseNumber, current.MaxNumber + 1, PromptDiff.Patch(inUse, normalised), Encoding.UTF8.GetByteCount(normalised), Warnings(key, normalised));
    }

    public IReadOnlyList<PromptWarningDto> Warnings(PromptKey key, string text) => PromptWarnings.Of(text, builtIns.Facts(key).Placeholders);
}
