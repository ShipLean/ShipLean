using System.Globalization;
using Dapper;
using Npgsql;
using ShipLean.Application.Features.Timeline.Facts.Sql;
using ShipLean.Application.Plans;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views.Timeline;

namespace ShipLean.Application.Features.Timeline.Facts;

// The facts over Postgres: one connection and one query per kind of fact for the whole tree. Removed stories, bugs and tasks
// (current category removed), and the tasks of a removed story or bug, are left out entirely.
public sealed class TimelineFactsReader(NpgsqlDataSource dataSource) : ITimelineFactsReader
{
    public async Task<TimelineFacts> ReadAsync(Guid projectId, Guid featureId, DateTimeOffset asOf, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        Task<IEnumerable<T>> Query<T>(string sql, object parameters)
            => connection.QueryAsync<T>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));

        var head = await connection.QuerySingleAsync<HeadRow>(new CommandDefinition(
            TimelineFactsSql.ProjectKeyAndEpic, new { projectId, featureId }, cancellationToken: cancellationToken));
        var items = (await Query<ItemRow>(TimelineFactsSql.Items, new { projectId, featureId })).ToList();
        var featureRow = items.Single(item => item.Id == featureId);
        var stories = items
            .Where(item => item.ParentId == featureId && item.StateCategory != StateCategory.Removed
                && Enum.Parse<WorkItemTypeId>(item.Type, ignoreCase: true) is WorkItemTypeId.Story or WorkItemTypeId.Bug)
            .ToList();
        var storyIds = stories.Select(story => story.Id).ToHashSet();
        var tasks = items
            .Where(item => item.ParentId is { } parent && storyIds.Contains(parent) && item.StateCategory != StateCategory.Removed
                && string.Equals(item.Type, "task", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var keyOf = items.ToDictionary(item => item.Id, item => item.Key);

        Guid[] ids = [featureId, .. stories.Select(story => story.Id), .. tasks.Select(task => task.Id)];
        Guid[] taskIds = [.. tasks.Select(task => task.Id)];

        var moves = (await Query<MoveRow>(TimelineFactsSql.Moves, new { ids, stateField = FieldIds.State }))
            .Select(row => new TimelineMoveFact(row.ItemKey, row.At, row.Rev, row.StateName, row.Category, row.AgentName, row.UserId)).ToList();
        var blocked = (await Query<BlockedRow>(TimelineFactsSql.BlockedTags, new { taskIds, tagsField = FieldIds.Tags }))
            .Select(row => new TimelineBlockedTagFact(row.ItemKey, row.At, row.Rev, row.BlockedAfter)).ToList();
        var links = (await Query<LinkRow>(TimelineFactsSql.Links, new { ids })).Select(Normalise).ToList();
        var appliedAt = await connection.ExecuteScalarAsync<DateTimeOffset?>(new CommandDefinition(
            TimelineFactsSql.PlanApplied, new { featureId }, cancellationToken: cancellationToken));
        var reviews = (await Query<ReviewRow>(TimelineFactsSql.Reviews, new { taskIds }))
            .Select(row => new TimelineReviewFact(
                row.ItemKey, row.At, Enum.Parse<ReviewVerdict>(row.Verdict, ignoreCase: true), row.Note, row.ByUserId, row.AgentName)).ToList();

        var taskKeys = tasks.Select(task => task.Key).ToHashSet();
        var sprints = await ReadSprintsAsync(Query<SprintRow>, projectId, moves.Where(move => taskKeys.Contains(move.ItemKey)), asOf);

        var waves = new Dictionary<string, string>();
        foreach (var story in stories)
        {
            if (PlanTags.Parse((string[])story.Tags).Wave is { } wave)
            {
                waves[story.Key] = wave.Text;
            }
        }

        var feature = new TimelineFeatureFact(
            featureRow.Id, featureRow.Key, featureRow.Title, featureRow.StateName, featureRow.StateCategory, featureRow.CreatedAt, featureRow.CreatedBy,
            head.EpicKey is null || head.EpicTitle is null ? null : new TimelineEpicFact(head.EpicKey, head.EpicTitle));
        var storyFacts = stories.Select(story => new TimelineStoryFact(
            story.Key, Enum.Parse<WorkItemTypeId>(story.Type, ignoreCase: true), story.Title, story.AssigneeId, story.CreatedAt, story.StateName, story.StateCategory)).ToList();
        var taskFacts = tasks.Select(task => new TimelineTaskFact(
            task.Key, keyOf[task.ParentId!.Value], task.Title, task.AssigneeId, task.StateName, task.StateCategory)).ToList();

        Guid[] userIds =
        [
            .. new[] { feature.CreatedBy }.Concat(storyFacts.Select(story => story.AssigneeId)).Concat(taskFacts.Select(task => task.AssigneeId))
                .Concat(moves.Select(move => move.UserId)).OfType<Guid>(),
            .. reviews.Select(review => review.ByUserId),
        ];
        var people = (await Query<PersonRow>(TimelineFactsSql.People, new { userIds = userIds.Distinct().ToArray() })).ToDictionary(row => row.Id, row => row.Name);

        return new TimelineFacts(head.ProjectKey, feature, appliedAt, storyFacts, taskFacts, moves, blocked, links, waves, reviews, sprints, people);
    }

    // From must finish before To: A successor B is A to B; A predecessor B and A blockedBy B are B to A.
    private static TimelineLinkFact Normalise(LinkRow row) => row.Kind switch
    {
        "successor" => new TimelineLinkFact(row.SourceKey, row.TargetKey, TimelineLinkKind.Successor),
        "predecessor" => new TimelineLinkFact(row.TargetKey, row.SourceKey, TimelineLinkKind.Predecessor),
        _ => new TimelineLinkFact(row.TargetKey, row.SourceKey, TimelineLinkKind.BlockedBy),
    };

    // Sprints overlapping the days from the earliest task move to asOf, both ends inclusive; none when no task moved.
    private static async Task<IReadOnlyList<TimelineSprintFact>> ReadSprintsAsync(
        Func<string, object, Task<IEnumerable<SprintRow>>> query, Guid projectId, IEnumerable<TimelineMoveFact> taskMoves, DateTimeOffset asOf)
    {
        var moveTimes = taskMoves.Select(move => move.At).ToList();
        if (moveTimes.Count == 0)
        {
            return [];
        }

        var rows = await query(TimelineFactsSql.Sprints, new { projectId, fromDay = Day(moveTimes.Min()), toDay = Day(asOf) });
        return [.. rows.Select(row => new TimelineSprintFact(row.Name, ParseDay(row.StartDate), ParseDay(row.EndDate)))];
    }

    private static string Day(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static DateOnly ParseDay(string day) => DateOnly.ParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private sealed record HeadRow(string ProjectKey, string? EpicKey, string? EpicTitle);

    // Tags is an Array: Npgsql reports text[] as one, and it is always a string[].
    private sealed record ItemRow(
        Guid Id, string Key, string Type, string Title, Guid? ParentId, Guid? AssigneeId, DateTimeOffset CreatedAt, Guid? CreatedBy,
        Array Tags, string StateName, StateCategory StateCategory);

    private sealed record MoveRow(string ItemKey, DateTimeOffset At, int Rev, string StateName, StateCategory Category, string? AgentName, Guid? UserId);

    private sealed record BlockedRow(string ItemKey, DateTimeOffset At, int Rev, bool BlockedAfter);

    private sealed record LinkRow(string SourceKey, string TargetKey, string Kind);

    private sealed record ReviewRow(string ItemKey, DateTimeOffset At, string Verdict, string? Note, Guid ByUserId, string? AgentName);

    private sealed record SprintRow(string Name, string StartDate, string EndDate);

    private sealed record PersonRow(Guid Id, string Name);
}
