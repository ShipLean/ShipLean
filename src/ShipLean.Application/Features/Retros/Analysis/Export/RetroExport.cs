using Dapper;
using Npgsql;
using ShipLean.Application.Features.Retros.Decide;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Retros.Analysis;
using ShipLean.Contracts.Retros.Decide;

namespace ShipLean.Application.Features.Retros.Analysis.Export;

// export_retro_points (C1): one page of a scope's points with the context an agent needs. One query per concern per page, never one per point.
internal sealed class RetroExport(NpgsqlDataSource dataSource) : IRetroExport
{
    private const int BriefLength = 2000;

    public async Task<RetroExportPage> ExportAsync(Guid projectId, DecideScope scope, RetroExportOptions options, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var filter = new
        {
            includeWentWell = options.IncludeWentWell,
            excludeCovered = options.ExcludeCovered,
            afterAt = options.After is { } after ? after.LoggedAt.UtcDateTime : (DateTime?)null,
            afterId = options.After?.PointId,
            take = options.Limit + 1,
        };
        var scopeArgs = DecideScopeSql.Args(projectId, scope);
        var parameters = new DynamicParameters(scopeArgs);
        parameters.AddDynamicParams(filter);

        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(RetroExportSql.Total, parameters, cancellationToken: cancellationToken));
        var rows = (await connection.QueryAsync<PointRow>(new CommandDefinition(RetroExportSql.Points, parameters, cancellationToken: cancellationToken))).ToList();
        var more = rows.Count > options.Limit;
        if (more)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var retroIds = rows.Select(row => row.RetroId).Distinct().ToArray();
        var itemIds = rows.Where(row => row.ItemId is not null).Select(row => row.ItemId!.Value).Distinct().ToArray();
        var retros = retroIds.Length == 0 ? new Dictionary<Guid, RetroExportRetro>() : await RetrosAsync(connection, retroIds, cancellationToken);
        var commits = itemIds.Length == 0 ? new Dictionary<Guid, List<RetroExportCommit>>() : await CommitsAsync(connection, itemIds, cancellationToken);
        var first = options.After is null;
        var points = rows.Select(row => Point(row, commits)).ToList();
        var last = rows.Count == 0 ? null : rows[^1];

        return new RetroExportPage(
            await ScopeAsync(connection, scope, cancellationToken),
            total,
            more && last is not null ? new RetroExportCursor(new DateTimeOffset(DateTime.SpecifyKind(last.LoggedAt, DateTimeKind.Utc)), last.Id) : null,
            points,
            retros,
            first ? await OpenActionsAsync(connection, projectId, cancellationToken) : [],
            first && (options.PatternsFromLatest || options.PatternsFromAnalysis is not null) ? await PatternsAsync(connection, projectId, options.PatternsFromAnalysis, cancellationToken) : []);
    }

    private static async Task<RetroScopeView> ScopeAsync(NpgsqlConnection connection, DecideScope scope, CancellationToken cancellationToken)
    {
        var names = await connection.QuerySingleAsync<(string? Feature, string? Sprint)>(
            new CommandDefinition(RetroExportSql.ScopeNames, new { featureId = scope.FeatureId, sprintId = scope.SprintId }, cancellationToken: cancellationToken));
        var kind = RetroScopeKind.Range;
        if (scope.FeatureId is not null)
        {
            kind = RetroScopeKind.Feature;
        }
        else if (scope.SprintId is not null)
        {
            kind = RetroScopeKind.Sprint;
        }

        return new RetroScopeView(kind, names.Feature, names.Sprint, scope.From, scope.To);
    }

    private static async Task<Dictionary<Guid, RetroExportRetro>> RetrosAsync(NpgsqlConnection connection, Guid[] retroIds, CancellationToken cancellationToken)
    {
        var retros = await connection.QueryAsync<RetroRow>(new CommandDefinition(RetroExportSql.Retros, new { retroIds }, cancellationToken: cancellationToken));
        var logs = (await connection.QueryAsync<FailureRow>(new CommandDefinition(RetroExportSql.FailureLogs, new { retroIds }, cancellationToken: cancellationToken))).ToLookup(row => row.RetroId);
        return retros.ToDictionary(
            retro => retro.Id,
            retro => new RetroExportRetro(
                retro.Item, retro.Sprint, retro.AgentName, retro.Role,
                new RetroExportFailureLog(Lines(logs[retro.Id], "blocker"), Lines(logs[retro.Id], "assumptionDisproved"), Lines(logs[retro.Id], "friction"))));
    }

    private static List<string> Lines(IEnumerable<FailureRow> rows, string kind) => [.. rows.Where(row => row.Kind == kind).Select(row => row.Text)];

    private static async Task<Dictionary<Guid, List<RetroExportCommit>>> CommitsAsync(NpgsqlConnection connection, Guid[] itemIds, CancellationToken cancellationToken)
    {
        var rows = await connection.QueryAsync<CommitRow>(new CommandDefinition(RetroExportSql.Commits, new { itemIds }, cancellationToken: cancellationToken));
        return rows.GroupBy(row => row.ItemId).ToDictionary(group => group.Key, group => group.Select(row => new RetroExportCommit(row.Repository, row.Sha, row.Subject, (string[])row.Paths)).ToList());
    }

    private static async Task<IReadOnlyList<RetroExportOpenAction>> OpenActionsAsync(NpgsqlConnection connection, Guid projectId, CancellationToken cancellationToken)
    {
        var rows = await connection.QueryAsync<OpenActionRow>(new CommandDefinition(RetroExportSql.OpenActions, new { projectId }, cancellationToken: cancellationToken));
        return [.. rows.Select(row => new RetroExportOpenAction(row.Key, row.Title, row.State, row.FixType, row.Theme, row.PointCount))];
    }

    // A named analysis of another project, or an archived one, is NotFound; `latest` with no analysis yet is an empty list.
    private static async Task<IReadOnlyList<RetroExportPattern>> PatternsAsync(NpgsqlConnection connection, Guid projectId, Guid? analysisId, CancellationToken cancellationToken)
    {
        var analysis = await connection.QuerySingleOrDefaultAsync<AnalysisRow>(new CommandDefinition(RetroExportSql.PatternsAnalysis, new { projectId, analysisId }, cancellationToken: cancellationToken));
        if (analysis is null)
        {
            return analysisId is null ? [] : throw ApiException.NotFound("No analysis with that id in this project.", "with_patterns_from");
        }

        var rows = await connection.QueryAsync<PatternRow>(new CommandDefinition(RetroExportSql.Patterns, new { analysisId = analysis.Id }, cancellationToken: cancellationToken));
        return [.. rows.Select(row => new RetroExportPattern(row.Id, Enum.Parse<RetroPatternKind>(row.Kind, ignoreCase: true), row.Title, row.Lesson, analysis.EmbeddingModel, row.Centroid is null ? [] : (float[])row.Centroid))];
    }

    private static RetroExportPoint Point(PointRow row, Dictionary<Guid, List<RetroExportCommit>> commits)
        => new(
            row.Id, row.RetroId, row.Category, row.Text, row.RootCause, row.Theme, new DateTimeOffset(DateTime.SpecifyKind(row.LoggedAt, DateTimeKind.Utc)), row.AgentName, row.Role,
            row.ItemId is null ? null : new RetroExportItem(row.ItemKey!, row.ItemType!, row.ItemTitle!, Brief(row.ItemBrief)),
            row.FeatureKey is null ? null : new RetroExportFeature(row.FeatureKey, row.FeatureTitle!, row.FeatureGoal),
            row.Sprint,
            row.ItemId is { } item && commits.TryGetValue(item, out var found) ? found : []);

    // At most 2,000 characters in all: the cut text ends with an ellipsis.
    private static string? Brief(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return text.Length <= BriefLength ? text : string.Concat(text.AsSpan(0, BriefLength - 1), "…");
    }

    private sealed record PointRow(
        Guid Id, Guid RetroId, string Category, string Text, string? RootCause, string? Theme, DateTime LoggedAt, string? AgentName, string Role,
        Guid? ItemId, string? ItemKey, string? ItemType, string? ItemTitle, string? ItemBrief, string? FeatureKey, string? FeatureTitle, string? FeatureGoal, string? Sprint);

    private sealed record RetroRow(Guid Id, string? Item, string? Sprint, string? AgentName, string Role);

    private sealed record FailureRow(Guid RetroId, string Kind, string Text);

    private sealed record CommitRow(Guid ItemId, string Repository, string Sha, string Subject, Array Paths);

    private sealed record OpenActionRow(string Key, string Title, string State, string? FixType, string? Theme, int PointCount);

    private sealed record AnalysisRow(Guid Id, string EmbeddingModel);

    private sealed record PatternRow(Guid Id, string Kind, string Title, string Lesson, Array? Centroid);
}
