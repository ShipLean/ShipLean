using System.Globalization;
using System.Text;
using Dapper;
using Npgsql;
using ShipLean.Application.Features.Mcp.Planning.Markers;
using ShipLean.Application.Features.WorkItems.Reads.Tree;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Mcp.Planning.Lessons;

// The "What to change next time" sections of a project's newest retros, from both places a retro lives: a live retro-*.md
// attachment and a live, not superseded "Retro · " comment on one of the project's Features. The stores list per work item
// only, so this reads across the project in SQL. Callers check project access first.
public sealed class ProjectLessons(NpgsqlDataSource dataSource, IAttachmentStore attachments)
{
    public const int Newest = 3;

    public async Task<IReadOnlyList<RetroLesson>> NewestAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var candidates = await CandidatesAsync(projectId, cancellationToken);

        // Newest first across both kinds; a retro with no section (or an empty one) is skipped before the cut, and so is an attachment
        // that cannot be opened (replaced or archived since the query, or its blob is gone): the next candidate takes its place.
        var lessons = new List<RetroLesson>();
        foreach (var candidate in candidates)
        {
            var text = MarkdownSections.Extract(candidate.Body ?? await ReadAsync(candidate.Id, cancellationToken) ?? string.Empty, PlanHeadings.Lessons);
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }
            var retro = candidate.Source ?? "comment " + candidate.CreatedAt.ToUniversalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            lessons.Add(new RetroLesson(candidate.Feature, retro, text));
            if (lessons.Count == Newest)
            {
                break;
            }
        }
        lessons.AddRange(await ImprovePointsAsync(projectId, cancellationToken));
        return lessons;
    }

    // The "improve" points of the newest conductor retrospectives logged on the project's live Features (log_retrospective), one lesson
    // each, after the Markdown ones. Archived points do not count, and neither does a retro left with none; sub-agent retros are not lessons.
    private async Task<List<RetroLesson>> ImprovePointsAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = (await connection.QueryAsync<ImproveRow>(new CommandDefinition(
            $"""
            SELECT w.key AS feature, r.id AS retroId, r.created_at AS createdAt, p.text, p.position
            FROM (
                SELECT r.id, r.work_item_id, r.created_at
                FROM retrospective r JOIN work_item w ON w.id = r.work_item_id JOIN state s ON s.id = w.state_id
                WHERE r.project_id = @projectId AND r.role = 'conductor' AND r.archived_at IS NULL AND w.type = 'feature' AND {TreeSql.Live}
                  AND EXISTS (SELECT 1 FROM retro_point p WHERE p.retrospective_id = r.id AND p.category = 'improve' AND p.archived_at IS NULL)
                ORDER BY r.created_at DESC, r.id
                LIMIT @newest
            ) r
            JOIN work_item w ON w.id = r.work_item_id
            JOIN retro_point p ON p.retrospective_id = r.id AND p.category = 'improve' AND p.archived_at IS NULL
            ORDER BY r.created_at DESC, r.id, p.position, p.created_at
            """,
            new { projectId, newest = Newest }, cancellationToken: cancellationToken))).ToList();

        return [.. rows.GroupBy(row => row.RetroId).Select(group =>
        {
            var first = group.First();
            var date = first.CreatedAt.ToUniversalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return new RetroLesson(first.Feature, "retrospective " + date, string.Join("\n", group.Select(row => "- " + row.Text)));
        })];
    }

    // Both queries run on one connection that is closed before any attachment is opened. Features that are Removed do not count: a
    // plan the team dropped does not feed lessons forward.
    private async Task<List<Candidate>> CandidatesAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        // A re-attached retro archives the old row, so a replaced retro is one live row. SQL narrows by prefix; the Plan* helpers decide.
        var files = (await connection.QueryAsync<Candidate>(new CommandDefinition(
            $"""
            SELECT a.id, w.key AS feature, a.name AS source, NULL::text AS body, a.created_at AS createdAt
            FROM attachment a JOIN work_item w ON w.id = a.work_item_id JOIN state s ON s.id = w.state_id
            WHERE w.project_id = @projectId AND w.type = 'feature' AND {TreeSql.Live} AND a.archived_at IS NULL AND lower(a.name) LIKE 'retro-%'
            """,
            new { projectId }, cancellationToken: cancellationToken))).Where(c => PlanFiles.IsRetro(c.Source!));
        var comments = (await connection.QueryAsync<Candidate>(new CommandDefinition(
            $"""
            SELECT c.id, w.key AS feature, NULL::text AS source, c.body, c.created_at AS createdAt
            FROM comment c JOIN work_item w ON w.id = c.work_item_id JOIN state s ON s.id = w.state_id
            WHERE w.project_id = @projectId AND w.type = 'feature' AND {TreeSql.Live} AND c.archived_at IS NULL AND c.superseded_at IS NULL AND c.body LIKE 'Retro · %'
            """,
            new { projectId }, cancellationToken: cancellationToken))).Where(c => PlanMarkers.IsRetro(c.Body!));

        return [.. files.Concat(comments).OrderByDescending(c => c.CreatedAt).ThenBy(c => c.Feature, StringComparer.Ordinal).ThenBy(c => c.Id)];
    }

    // Null when the attachment is gone: NotFound from the store means archived, replaced or its bytes missing.
    private async Task<string?> ReadAsync(Guid attachmentId, CancellationToken cancellationToken)
    {
        try
        {
            var (_, content) = await attachments.OpenAsync(attachmentId, cancellationToken);
            using (content.Content)
            {
                using var reader = new StreamReader(content.Content, Encoding.UTF8);
                return await reader.ReadToEndAsync(cancellationToken);
            }
        }
        catch (ApiException e) when (e.Code == ApiErrorCodes.NotFound)
        {
            return null;
        }
    }

    private sealed record ImproveRow(string Feature, Guid RetroId, DateTime CreatedAt, string Text, int Position);

    private sealed record Candidate(Guid Id, string Feature, string? Source, string? Body, DateTime CreatedAt);
}
