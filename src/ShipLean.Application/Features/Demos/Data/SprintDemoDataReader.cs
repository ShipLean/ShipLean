using System.Globalization;
using System.Text.Json;
using Dapper;
using Npgsql;
using ShipLean.Application.Features.Mcp.Backlog;
using ShipLean.Application.Features.Mcp.Planning.Markers;
using ShipLean.Application.Features.Mcp.Planning.Tags;
using ShipLean.Application.Features.Mcp.Planning.Waves;
using ShipLean.Contracts.Demos;
using ShipLean.Contracts.Persisted;

namespace ShipLean.Application.Features.Demos.Data;

// K7: what a sprint demo needs to know, per Feature, so the agent needn't call get_work_item. The SQL is SprintDemoDataSql's and reads no
// failure log, root cause, comment body or author. Anything cut to a limit sets Truncated.
public sealed class SprintDemoDataReader(NpgsqlDataSource dataSource, BacklogReads backlog) : ISprintDemoData
{
    internal const int MaxWhyLength = 4096;
    internal const int MaxStoriesPerFeature = 50;
    internal const int MaxSummaryLength = 600;
    internal const int MaxCriteria = 10;
    internal const int MaxTasksPerStory = 30;
    internal const int MaxRetroPointsPerFeature = 40;
    internal const int MaxPointTextLength = 400;

    private static readonly string[] StoryTypes = ["story", "bug"];
    private static readonly string[] Categories = ["wentWell", "wentBadly", "improve", "notInPlan"];

    public async Task<SprintDemoData> ReadAsync(Guid sprintId, CancellationToken cancellationToken = default)
    {
        var featureIds = (await backlog.FeaturesAsync(sprintId, cancellationToken)).Select(feature => feature.Id).ToArray();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var truncation = new Truncation();

        var head = await connection.QuerySingleAsync<HeadRow>(new CommandDefinition(SprintDemoDataSql.SprintAndProject, new { sprintId }, cancellationToken: cancellationToken));
        var featureRows = (await connection.QueryAsync<FeatureRow>(new CommandDefinition(SprintDemoDataSql.Features, new { featureIds }, cancellationToken: cancellationToken))).ToList();
        var stories = (await connection.QueryAsync<StoryRow>(new CommandDefinition(
            SprintDemoDataSql.Stories, new { sprintId, types = StoryTypes, summaryLength = MaxSummaryLength }, cancellationToken: cancellationToken))).ToList();
        var storyIds = stories.Select(story => story.Id).ToArray();
        var tasks = (await connection.QueryAsync<TaskRow>(new CommandDefinition(SprintDemoDataSql.Tasks, new { storyIds }, cancellationToken: cancellationToken))).ToLookup(task => task.StoryId);
        var plan = await connection.QueryAsync<PlanRow>(new CommandDefinition(SprintDemoDataSql.PlanShape, new { featureIds }, cancellationToken: cancellationToken));
        var reviews = (await connection.QueryAsync<ReviewRow>(new CommandDefinition(
            SprintDemoDataSql.PlanReviews, new { featureIds, reviewPrefix = PlanMarkers.ReviewPrefix + "%" }, cancellationToken: cancellationToken))).ToDictionary(row => row.FeatureId, row => row.Rounds);
        var pullRequests = (await connection.QueryAsync<PullRequestRow>(new CommandDefinition(
            SprintDemoDataSql.PullRequests, new { featureIds, pullRequestPrefix = PlanMarkers.PullRequestPrefix + "%" }, cancellationToken: cancellationToken))).ToLookup(row => row.FeatureId, row => row.Url);
        var attachments = (await connection.QueryAsync<AttachmentRow>(new CommandDefinition(SprintDemoDataSql.Attachments, new { featureIds }, cancellationToken: cancellationToken))).ToLookup(row => row.FeatureId, row => row.Name);
        var points = (await connection.QueryAsync<PointRow>(new CommandDefinition(
            SprintDemoDataSql.RetroPoints, new { sprintId, textLength = MaxPointTextLength }, cancellationToken: cancellationToken))).ToList();
        var actions = await connection.QueryAsync<ActionRow>(new CommandDefinition(SprintDemoDataSql.Actions, new { sprintId }, cancellationToken: cancellationToken));

        var shown = featureRows.Select(row => row.Id).ToHashSet();
        var features = featureRows.Select(row => Feature(
            row, head.ProjectKey, stories.Where(story => story.FeatureId == row.Id).ToList(), tasks, plan.Where(member => member.FeatureId == row.Id).ToList(),
            reviews.GetValueOrDefault(row.Id), [.. pullRequests[row.Id].Distinct()], [.. attachments[row.Id]],
            points.Where(point => point.FeatureId == row.Id).ToList(), truncation)).ToList();
        var otherWork = stories.Where(story => story.FeatureId is null).Take(MaxStoriesPerFeature).Select(story => Story(story, tasks, truncation)).ToList();
        truncation.Mark(stories.Count(story => story.FeatureId is null) > MaxStoriesPerFeature);

        return SprintDemoDataBudget.Apply(new SprintDemoData(
            new SprintDemoSprint(head.SprintName, head.SprintGoal, DateOnly.ParseExact(head.StartDate, "yyyy-MM-dd", CultureInfo.InvariantCulture), DateOnly.ParseExact(head.EndDate, "yyyy-MM-dd", CultureInfo.InvariantCulture), head.Closed),
            new SprintDemoProject(head.ProjectKey, head.ProjectName, head.RepoUrl, head.BaseBranch, head.Environment),
            features,
            otherWork,
            Retro(points.Where(point => point.FeatureId is null || !shown.Contains(point.FeatureId.Value)).ToList(), truncation),
            [.. actions.Select(action => new SprintDemoActionLine(action.Key, action.Title, action.State))],
            truncation.Happened));
    }

    private static SprintDemoFeatureData Feature(
        FeatureRow row, string projectKey, List<StoryRow> stories, ILookup<Guid, TaskRow> tasks, List<PlanRow> members, int reviewRounds,
        IReadOnlyList<string> pullRequests, IReadOnlyList<string> attachmentNames, List<PointRow> points, Truncation truncation)
    {
        var parts = DemoWhy.Extract(row.Description);
        var why = parts.Why;
        if (why is not null && why.Length > MaxWhyLength)
        {
            why = why[..MaxWhyLength];
            truncation.Mark(true);
        }
        truncation.Mark(stories.Count > MaxStoriesPerFeature);
        return new SprintDemoFeatureData(
            row.Key, row.Title, row.State, $"/p/{projectKey}/items/{row.Key}", row.Goal, why, parts.Measure,
            [.. stories.Take(MaxStoriesPerFeature).Select(story => Story(story, tasks, truncation))],
            PlanFacts(members, reviewRounds, pullRequests),
            Retro(points, truncation, MaxRetroPointsPerFeature),
            attachmentNames,
            pullRequests);
    }

    // A plan is the Feature's live stories. Its waves are the distinct wave: tags; a story without one is a wave of its own, as PlanReads reads it.
    private static SprintDemoPlanFacts? PlanFacts(List<PlanRow> members, int reviewRounds, IReadOnlyList<string> pullRequests)
    {
        var planStories = members.Where(member => member.Kind == "story").Select(member => PlanTags.Parse((string[])member.Tags).Wave).ToList();
        if (planStories.Count == 0)
        {
            return null;
        }
        var waves = planStories.OfType<WaveLabel>().Select(wave => wave.Text).Distinct().Count() + planStories.Count(wave => wave is null);
        return new SprintDemoPlanFacts(waves, planStories.Count, members.Count(member => member.Kind == "task"), reviewRounds, pullRequests);
    }

    private static SprintDemoStoryLine Story(StoryRow row, ILookup<Guid, TaskRow> tasks, Truncation truncation)
    {
        var summary = string.IsNullOrWhiteSpace(row.Summary) ? null : row.Summary.Trim();
        if (summary is not null && summary.Length > MaxSummaryLength)
        {
            summary = summary[..MaxSummaryLength];
            truncation.Mark(true);
        }
        var criteria = (JsonSerializer.Deserialize<List<ChecklistItem>>(row.Criteria, PersistedJson.Options) ?? [])
            .Select(item => item.Text.Trim()).Where(text => text.Length > 0).ToList();
        truncation.Mark(criteria.Count > MaxCriteria);
        var taskLines = tasks[row.Id].ToList();
        truncation.Mark(taskLines.Count > MaxTasksPerStory);
        return new SprintDemoStoryLine(
            row.Key, row.Type, row.Title, row.State, row.Points, summary,
            [.. criteria.Take(MaxCriteria)],
            [.. taskLines.Take(MaxTasksPerStory).Select(task => new SprintDemoTaskLine(task.Title, task.State))]);
    }

    private static SprintDemoRetroData Retro(List<PointRow> points, Truncation truncation, int limit = MaxRetroPointsPerFeature)
    {
        truncation.Mark(points.Count > limit);
        var kept = points.Take(limit).ToList();
        IReadOnlyList<SprintDemoRetroPoint> Of(string category) => [.. kept.Where(point => point.Category == category).Select(point => Point(point, truncation))];
        return new SprintDemoRetroData(Of(Categories[0]), Of(Categories[1]), Of(Categories[2]), Of(Categories[3]));
    }

    private static SprintDemoRetroPoint Point(PointRow row, Truncation truncation)
    {
        var text = row.Text;
        if (text.Length > MaxPointTextLength)
        {
            text = text[..MaxPointTextLength];
            truncation.Mark(true);
        }
        return new SprintDemoRetroPoint(row.Theme, text);
    }

    private sealed class Truncation
    {
        public bool Happened { get; private set; }

        public void Mark(bool cut) => Happened |= cut;
    }

    private sealed record HeadRow(string SprintName, string SprintGoal, string StartDate, string EndDate, bool Closed, string ProjectKey, string ProjectName, string? RepoUrl, string? BaseBranch, string Environment);

    private sealed record FeatureRow(Guid Id, string Key, string Title, string State, string Goal, string Description);

    private sealed record StoryRow(Guid Id, Guid? FeatureId, string Key, string Type, string Title, string State, decimal? Points, string Summary, string Criteria);

    private sealed record TaskRow(Guid StoryId, string Title, string State);

    private sealed record PlanRow(Guid FeatureId, string Kind, Array Tags);

    private sealed record ReviewRow(Guid FeatureId, int Rounds);

    private sealed record PullRequestRow(Guid FeatureId, string Url);

    private sealed record AttachmentRow(Guid FeatureId, string Name);

    private sealed record PointRow(Guid? FeatureId, string Category, string Text, string? Theme);

    private sealed record ActionRow(string Key, string Title, string State);
}
