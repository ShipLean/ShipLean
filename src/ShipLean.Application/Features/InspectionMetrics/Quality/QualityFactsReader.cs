using System.Globalization;
using System.Text.Json;
using Dapper;
using Npgsql;
using ShipLean.Application.Features.InspectionMetrics.Facts;
using ShipLean.Application.Features.InspectionMetrics.Facts.Sql;
using ShipLean.Application.Features.InspectionMetrics.Quality.Sql;
using ShipLean.Application.Plans;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views.InspectionMetrics;
using ShipLean.Contracts.Views.Timeline;

namespace ShipLean.Application.Features.InspectionMetrics.Quality;

// The quality facts over Postgres (QK4): whether the project has any live bug, the live bugs filed in the span with their first live
// Caused by Task, the PRs linked to a live item of the project that merged in the span, the project's severity weights, and the
// lead-time facts of every cause Task (its moves, Blocked tag changes, reviews and commits whatever their dates). The span runs in UTC
// from (fromDay - 1 day) 00:00Z up to (toDay + 3 days) 00:00Z, as the lead-time read: the aggregator places each bug and PR in its
// sprint in the viewer's zone.
public sealed class QualityFactsReader(NpgsqlDataSource dataSource) : IQualityFactsReader
{
    private const string Removed = "removed";
    private const string CausedBy = "causedBy";

    public async Task<BugFacts> ReadAsync(Guid projectId, DateOnly fromDay, DateOnly toDay, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        Task<IEnumerable<T>> Query<T>(string sql, object parameters) => QueryAsync<T>(connection, sql, parameters, ct);

        var span = new { projectId, fromDay = Day(fromDay), toDay = Day(toDay), removed = Removed, causedBy = CausedBy };
        var anyBugs = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(QualityFactsSql.AnyBugs, new { projectId, removed = Removed }, cancellationToken: ct));
        var bugRows = (await Query<BugRow>(QualityFactsSql.Bugs, span)).ToList();
        var prs = (await Query<PrRow>(QualityFactsSql.MergedPrs, span)).Select(row => new MergedPrFact(row.Url, row.MergedAt)).ToList();
        var weightsJson = await connection.ExecuteScalarAsync<string>(new CommandDefinition(QualityFactsSql.SeverityWeights, new { projectId }, cancellationToken: ct));
        var weights = weightsJson is null
            ? new Dictionary<int, int>()
            : JsonSerializer.Deserialize<Dictionary<string, int>>(weightsJson)!.ToDictionary(pair => int.Parse(pair.Key, CultureInfo.InvariantCulture), pair => pair.Value);

        Guid[] causeIds = [.. bugRows.Select(row => row.CauseId).OfType<Guid>().Distinct()];
        var causes = causeIds.Length == 0 ? LeadTimeFacts.Empty : await ReadCausesAsync(connection, causeIds, ct);

        var bugs = bugRows.Select(row => new BugFact(row.Key, row.Title, row.Severity, row.FiledAt, row.FoundBeforeRelease, row.CausedByKey)).ToList();
        return new BugFacts(anyBugs, bugs, prs, weights, causes);
    }

    private static Task<IEnumerable<T>> QueryAsync<T>(NpgsqlConnection connection, string sql, object parameters, CancellationToken ct)
        => connection.QueryAsync<T>(new CommandDefinition(sql, parameters, cancellationToken: ct));

    // The cause Tasks' lead-time facts, shaped as the lead-time reader shapes them, over every date.
    private static async Task<LeadTimeFacts> ReadCausesAsync(NpgsqlConnection connection, Guid[] taskIds, CancellationToken ct)
    {
        var taskRows = (await QueryAsync<TaskRow>(connection, QualityFactsSql.CauseTasks, new { taskIds, stateField = FieldIds.State, inProgress = "inProgress" }, ct)).ToList();
        var moves = (await QueryAsync<MoveRow>(connection, LeadTimeFactsSql.Moves, new { taskIds, stateField = FieldIds.State }, ct))
            .Select(row => new LeadTimeMoveFact(row.ItemKey, row.At, row.Rev, row.StateName, row.Category, row.AgentName, row.UserId)).ToList();
        var blocked = (await QueryAsync<BlockedRow>(connection, LeadTimeFactsSql.BlockedTags, new { taskIds, tagsField = FieldIds.Tags }, ct))
            .Select(row => new LeadTimeBlockedTagFact(row.ItemKey, row.At, row.Rev, row.BlockedAfter)).ToList();
        var reviews = (await QueryAsync<ReviewRow>(connection, LeadTimeFactsSql.Reviews, new { taskIds }, ct))
            .Select(row => new LeadTimeReviewFact(row.ItemKey, row.At, Enum.Parse<ReviewVerdict>(row.Verdict, ignoreCase: true))).ToList();
        var commits = (await QueryAsync<CommitRow>(connection, LeadTimeFactsSql.Commits, new { taskIds, commitPrefix = PlanMarkers.CommitPrefix }, ct))
            .Select(row => new LeadTimeCommitFact(row.ItemKey, row.Sha7, row.CommittedAt)).ToList();

        Guid[] userIds = [.. taskRows.Select(row => row.AssigneeId).Concat(moves.Select(move => move.UserId)).OfType<Guid>().Distinct()];
        var people = (await QueryAsync<PersonRow>(connection, LeadTimeFactsSql.People, new { userIds }, ct)).ToDictionary(row => row.Id, row => row.Name);

        var tasks = taskRows.Select(row => new LeadTimeTaskFact(
            row.Key, row.Title, row.ParentKey, ParentTypeOf(row.ParentType), row.FeatureKey, row.FeatureTitle, row.AssigneeId,
            row.AssigneeId is { } assignee && people.TryGetValue(assignee, out var name) ? name : null,
            row.AgentName, row.StateName, row.StateCategory)).ToList();
        return new LeadTimeFacts(tasks, moves, blocked, reviews, commits, people);
    }

    private static InspectionParentType? ParentTypeOf(string? type) => type switch
    {
        null => null,
        "bug" => InspectionParentType.Bug,
        _ => InspectionParentType.Story,
    };

    private static string Day(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private sealed record BugRow(string Key, string Title, int? Severity, DateTimeOffset FiledAt, bool FoundBeforeRelease, Guid? CauseId, string? CausedByKey);

    private sealed record PrRow(string Url, DateTimeOffset MergedAt);

    private sealed record TaskRow(
        Guid Id, string Key, string Title, string? ParentKey, string? ParentType, string? FeatureKey, string? FeatureTitle, Guid? AssigneeId,
        string? AgentName, string StateName, StateCategory StateCategory);

    private sealed record MoveRow(string ItemKey, DateTimeOffset At, int Rev, string StateName, StateCategory Category, string? AgentName, Guid? UserId);

    private sealed record BlockedRow(string ItemKey, DateTimeOffset At, int Rev, bool BlockedAfter);

    private sealed record ReviewRow(string ItemKey, DateTimeOffset At, string Verdict);

    private sealed record CommitRow(string ItemKey, string Sha7, DateTimeOffset? CommittedAt);

    private sealed record PersonRow(Guid Id, string Name);
}
