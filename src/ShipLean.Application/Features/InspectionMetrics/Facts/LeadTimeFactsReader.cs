using System.Globalization;
using Dapper;
using Npgsql;
using ShipLean.Application.Features.InspectionMetrics.Facts.Sql;
using ShipLean.Application.Plans;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views.InspectionMetrics;
using ShipLean.Contracts.Views.Timeline;

namespace ShipLean.Application.Features.InspectionMetrics.Facts;

// The lead-time facts over Postgres (K5): first the tasks whose lead time could end in the window, then every move, Blocked tag
// change, review and commit of those tasks, whatever its date. Days are UTC days.
public sealed class LeadTimeFactsReader(NpgsqlDataSource dataSource) : ILeadTimeFactsReader
{
    public async Task<LeadTimeFacts> ReadAsync(Guid projectId, DateOnly fromDay, DateOnly toDay, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        Task<IEnumerable<T>> Query<T>(string sql, object parameters)
            => connection.QueryAsync<T>(new CommandDefinition(sql, parameters, cancellationToken: ct));

        var taskRows = (await Query<TaskRow>(LeadTimeFactsSql.Tasks, new
        {
            projectId,
            fromDay = Day(fromDay),
            toDay = Day(toDay),
            stateField = FieldIds.State,
            inProgress = "inProgress",
            resolved = "resolved",
            removed = "removed",
            commitPrefix = PlanMarkers.CommitPrefix,
        })).ToList();
        if (taskRows.Count == 0)
        {
            return LeadTimeFacts.Empty;
        }

        Guid[] taskIds = [.. taskRows.Select(row => row.Id)];
        var moves = (await Query<MoveRow>(LeadTimeFactsSql.Moves, new { taskIds, stateField = FieldIds.State }))
            .Select(row => new LeadTimeMoveFact(row.ItemKey, row.At, row.Rev, row.StateName, row.Category, row.AgentName, row.UserId)).ToList();
        var blocked = (await Query<BlockedRow>(LeadTimeFactsSql.BlockedTags, new { taskIds, tagsField = FieldIds.Tags }))
            .Select(row => new LeadTimeBlockedTagFact(row.ItemKey, row.At, row.Rev, row.BlockedAfter)).ToList();
        var reviews = (await Query<ReviewRow>(LeadTimeFactsSql.Reviews, new { taskIds }))
            .Select(row => new LeadTimeReviewFact(row.ItemKey, row.At, Enum.Parse<ReviewVerdict>(row.Verdict, ignoreCase: true))).ToList();
        var commits = (await Query<CommitRow>(LeadTimeFactsSql.Commits, new { taskIds, commitPrefix = PlanMarkers.CommitPrefix }))
            .Select(row => new LeadTimeCommitFact(row.ItemKey, row.Sha7, row.CommittedAt)).ToList();

        Guid[] userIds = [.. taskRows.Select(row => row.AssigneeId).Concat(moves.Select(move => move.UserId)).OfType<Guid>().Distinct()];
        var people = (await Query<PersonRow>(LeadTimeFactsSql.People, new { userIds })).ToDictionary(row => row.Id, row => row.Name);

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

    private sealed record TaskRow(
        Guid Id, string Key, string Title, string? ParentKey, string? ParentType, string? FeatureKey, string? FeatureTitle, Guid? AssigneeId,
        string? AgentName, string StateName, StateCategory StateCategory);

    private sealed record MoveRow(string ItemKey, DateTimeOffset At, int Rev, string StateName, StateCategory Category, string? AgentName, Guid? UserId);

    private sealed record BlockedRow(string ItemKey, DateTimeOffset At, int Rev, bool BlockedAfter);

    private sealed record ReviewRow(string ItemKey, DateTimeOffset At, string Verdict);

    private sealed record CommitRow(string ItemKey, string Sha7, DateTimeOffset? CommittedAt);

    private sealed record PersonRow(Guid Id, string Name);
}
