using System.Text;
using System.Text.Json;
using Dapper;
using Npgsql;
using ShipLean.Application.Markdown;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Demos;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;

namespace ShipLean.Application.Features.Demos.Reads;

// K4 and K6's read: the sprint's row, its latest session, the files its pointers name (each opened once), and the features its chapters name.
public sealed class SprintDemoReader(
    NpgsqlDataSource dataSource,
    IAttachmentStore attachments,
    MarkdownRenderer renderer,
    IAgentSessionStarter sessions,
    TimeProvider time) : ISprintDemoReader
{
    private const string LiveSessionMessage = "An agent session is already making this demo.";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<SprintDemoDto> GetAsync(Guid sprintId, CancellationToken cancellationToken = default)
    {
        var now = time.GetUtcNow();
        SprintDemoRow row;
        SessionRow? session;
        IReadOnlyDictionary<Guid, FileRow> files;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        {
            row = await connection.QuerySingleOrDefaultAsync<SprintDemoRow>(new CommandDefinition(
                $"SELECT {SprintDemoRow.Columns} {SprintDemoRow.From} WHERE s.id = @sprintId", new { sprintId }, cancellationToken: cancellationToken))
                ?? throw ApiException.NotFound("That sprint was not found.");
            session = row.DemoWorkItemId is { } itemId ? await LatestSessionAsync(connection, itemId, cancellationToken) : null;
            files = await FilesAsync(connection, row, cancellationToken);
        }

        var live = session is { Live: true };
        var facts = new SprintDemoFacts(row.Closed, row.RequestedAt, row.BlockedMessage, live, row.DeliveredAt, row.UploadExpiresAt, session?.State, session?.EndedAt);
        var (status, message) = SprintDemoStatusRule.Decide(facts, now);

        var chapters = await ChaptersAsync(row, files, cancellationToken);
        var video = VideoOf(row, files);
        return new SprintDemoDto(
            row.Id,
            row.Name,
            status,
            message,
            RunOf(row, session, now),
            video,
            [.. chapters.Select(c => new SprintDemoChapterDto(c.Title, c.StartSeconds, SprintDemoLabels.Duration(c.StartSeconds), c.FeatureKey))],
            await FeaturesAsync(row, chapters, cancellationToken),
            await ScriptAsync(row, files, cancellationToken),
            YouTubeOf(row),
            await ActionsAsync(row, live, cancellationToken));
    }

    private static async Task<SessionRow?> LatestSessionAsync(NpgsqlConnection connection, Guid itemId, CancellationToken cancellationToken)
        => await connection.QuerySingleOrDefaultAsync<SessionRow>(new CommandDefinition(
            $"""
            SELECT state, state IN {AgentSessionLiveStates.Sql} AS live, coalesce(ended_at, last_activity_at) AS endedAt
            FROM agent_session WHERE work_item_id = @itemId
            ORDER BY (state IN {AgentSessionLiveStates.Sql}) DESC, created_at DESC LIMIT 1
            """,
            new { itemId }, cancellationToken: cancellationToken));

    // Only live files of the demo's own item: a pointer to an archived file, or to another item's, reads as no file.
    private static async Task<IReadOnlyDictionary<Guid, FileRow>> FilesAsync(NpgsqlConnection connection, SprintDemoRow row, CancellationToken cancellationToken)
    {
        Guid[] ids = [.. new[] { row.VideoAttachmentId, row.ScriptAttachmentId, row.ChaptersAttachmentId }.OfType<Guid>()];
        if (row.DemoWorkItemId is not { } itemId || ids.Length == 0)
        {
            return new Dictionary<Guid, FileRow>();
        }

        return (await connection.QueryAsync<FileRow>(new CommandDefinition(
            "SELECT id, name, size_bytes AS sizeBytes FROM attachment WHERE id = ANY(@ids) AND work_item_id = @itemId AND archived_at IS NULL",
            new { ids, itemId }, cancellationToken: cancellationToken))).ToDictionary(f => f.Id);
    }

    private SprintDemoRunDto? RunOf(SprintDemoRow row, SessionRow? session, DateTimeOffset now)
    {
        if (row.ItemKey is null)
        {
            return null;
        }

        var state = session is null ? null : SessionStateOf(session);
        var requested = row.RequestedAt is { } at ? $"Requested {SprintDemoLabels.When(at, time)}" : "Not requested yet";
        return new SprintDemoRunDto(row.ItemKey, $"/p/{row.ProjectKey}/items/{row.ItemKey}", state, requested, ExpiryLabel(row.UploadExpiresAt, now));
    }

    private static string SessionStateOf(SessionRow session) => session.Live ? session.State : "ended";

    private string? ExpiryLabel(DateTimeOffset? expires, DateTimeOffset now)
    {
        if (expires is not { } at)
        {
            return null;
        }

        return at <= now ? $"Link expired {SprintDemoLabels.When(at, time)}" : $"Link valid until {SprintDemoLabels.When(at, time)}";
    }

    private SprintDemoVideoDto? VideoOf(SprintDemoRow row, IReadOnlyDictionary<Guid, FileRow> files)
    {
        if (row.VideoAttachmentId is not { } id || !files.TryGetValue(id, out var file))
        {
            return null;
        }

        var duration = row.DurationSeconds ?? 0m;
        return new SprintDemoVideoDto(
            id,
            $"/api/attachments/{id}/content",
            file.Name,
            file.SizeBytes,
            duration,
            SprintDemoLabels.Duration(duration),
            row.DeliveredAt is { } delivered ? $"Delivered {SprintDemoLabels.When(delivered, time)}" : string.Empty);
    }

    // A missing, archived or unreadable chapters file is no chapters, not an error.
    private async Task<IReadOnlyList<DemoChapterJson>> ChaptersAsync(SprintDemoRow row, IReadOnlyDictionary<Guid, FileRow> files, CancellationToken cancellationToken)
    {
        var text = await ReadTextAsync(row.ChaptersAttachmentId, files, cancellationToken);
        if (text is null)
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<DemoChaptersJson>(text, Json)?.Chapters?.Where(c => c is not null).ToList() ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private async Task<string?> ScriptAsync(SprintDemoRow row, IReadOnlyDictionary<Guid, FileRow> files, CancellationToken cancellationToken)
    {
        var text = await ReadTextAsync(row.ScriptAttachmentId, files, cancellationToken);
        return text is null ? null : renderer.RenderFile(text).Html;
    }

    private async Task<string?> ReadTextAsync(Guid? id, IReadOnlyDictionary<Guid, FileRow> files, CancellationToken cancellationToken)
    {
        if (id is not { } attachmentId || !files.ContainsKey(attachmentId))
        {
            return null;
        }

        try
        {
            var (_, content) = await attachments.OpenAsync(attachmentId, cancellationToken);
            await using (content.Content)
            {
                using var reader = new StreamReader(content.Content, new UTF8Encoding(false, true));
                return await reader.ReadToEndAsync(cancellationToken);
            }
        }
        catch (ApiException failure) when (failure.Code == ApiErrorCodes.NotFound)
        {
            return null;
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    // The distinct feature keys of the chapters in chapter order, joined to the project's items in one query; an item in a removed state is left out.
    private async Task<IReadOnlyList<SprintDemoFeatureDto>> FeaturesAsync(SprintDemoRow row, IReadOnlyList<DemoChapterJson> chapters, CancellationToken cancellationToken)
    {
        string[] keys = [.. chapters.Select(c => c.FeatureKey).OfType<string>().Distinct()];
        if (keys.Length == 0)
        {
            return [];
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var found = (await connection.QueryAsync<FeatureRow>(new CommandDefinition(
            """
            SELECT w.key, w.title, st.id AS stateId, st.name AS stateName, st.category AS stateCategory
            FROM work_item w JOIN state st ON st.id = w.state_id
            WHERE w.project_id = @projectId AND w.key = ANY(@keys) AND st.category <> 'removed'
            """,
            new { projectId = row.ProjectId, keys }, cancellationToken: cancellationToken))).ToDictionary(f => f.Key);
        return [.. keys.Where(found.ContainsKey).Select(key => found[key]).Select(f => new SprintDemoFeatureDto(
            f.Key, f.Title, new StateRef(f.StateId, f.StateName, Enum.Parse<StateCategory>(f.StateCategory, ignoreCase: true)), $"/p/{row.ProjectKey}/items/{f.Key}"))];
    }

    private static SprintDemoYouTubeDto YouTubeOf(SprintDemoRow row)
    {
        var state = Enum.Parse<YouTubeDemoState>(row.YouTubeState, ignoreCase: true);
        if (state != YouTubeDemoState.Published || string.IsNullOrEmpty(row.YouTubeVideoId))
        {
            return new SprintDemoYouTubeDto(state, null, null, null, state == YouTubeDemoState.Failed ? row.YouTubeFailure : null);
        }

        var privacy = row.YouTubePrivacy is null ? (YouTubeVisibility?)null : Enum.Parse<YouTubeVisibility>(row.YouTubePrivacy, ignoreCase: true);
        var id = Uri.EscapeDataString(row.YouTubeVideoId);
        return new SprintDemoYouTubeDto(
            state,
            $"https://www.youtube.com/watch?v={id}",
            privacy == YouTubeVisibility.Private ? null : $"https://www.youtube-nocookie.com/embed/{id}",
            privacy,
            null);
    }

    private async Task<SprintDemoActionsDto> ActionsAsync(SprintDemoRow row, bool live, CancellationToken cancellationToken)
    {
        var retry = row.YouTubeState == "failed" && row.VideoAttachmentId is not null;
        if (!row.Closed)
        {
            return new SprintDemoActionsDto(false, SprintDemoStatusRule.CloseFirst, retry);
        }

        if (live)
        {
            return new SprintDemoActionsDto(false, LiveSessionMessage, retry);
        }

        return await sessions.PlanProjectAsync(row.ProjectId, null, cancellationToken) is { } block
            ? new SprintDemoActionsDto(false, AgentStartMessages.For(block), retry)
            : new SprintDemoActionsDto(true, null, retry);
    }

    private sealed record SessionRow(string State, bool Live, DateTimeOffset EndedAt);

    private sealed record FileRow(Guid Id, string Name, long SizeBytes);

    private sealed record FeatureRow(string Key, string Title, Guid StateId, string StateName, string StateCategory);
}
