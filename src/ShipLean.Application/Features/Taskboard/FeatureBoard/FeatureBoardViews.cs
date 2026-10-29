using Agoda.IoC.Core;
using Dapper;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Boards;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views;
using ShipLean.Contracts.WorkItems.Reads.Tree;

namespace ShipLean.Application.Features.Taskboard.FeatureBoard;

// A Feature's board: the Taskboard grid for the Feature's tree. Rows are the stories and bugs under the Feature and its nested
// Features, from any sprint or the Backlog, grouped in sections; cells hold their live tasks. Every number is counted from the
// rows returned (a section counts its own rows only), so the header always matches the board. Callers read it as a member.
[RegisterPerRequest]
public sealed class FeatureBoardViews(
    IProjectAccess access, IBoardSettingsStore boards, IWorkTreeReads tree, TaskboardTasks tasks, TaskboardCards cards, NpgsqlDataSource dataSource)
{
    public async Task<FeatureBoardViewDto> GetAsync(Guid projectId, string itemKey, CancellationToken cancellationToken)
    {
        await access.RequireAsync(projectId, ProjectRole.Member, cancellationToken);
        var root = (await tree.StackAsync(projectId, StackLevel.Feature, null, true, cancellationToken))
            .FirstOrDefault(f => string.Equals(f.Key, itemKey, StringComparison.OrdinalIgnoreCase))
            ?? throw ApiException.NotFound("Feature not found.");
        var summary = (await boards.ListAsync(projectId, cancellationToken)).FirstOrDefault(b => b.Level == BoardLevel.Taskboard)
            ?? throw ApiException.NotFound("The project has no Taskboard.");
        var board = await boards.GetAsync(summary.Id, cancellationToken);

        var descendants = await tree.DescendantsAsync(projectId, root.Id, null, cancellationToken);
        var layout = FeatureBoardTree.Layout(root, descendants);
        var stack = (await tree.StackAsync(projectId, StackLevel.Requirement, null, true, cancellationToken)).ToDictionary(r => r.Id);
        // A story removed between the two reads is skipped rather than failing the board.
        var rowOrder = layout.SelectMany(s => s.StoryIds.Select(id => (Section: s.Id, Story: id))).Where(r => stack.ContainsKey(r.Story)).ToList();
        var storyIds = rowOrder.Select(r => r.Story).ToList();
        var parents = storyIds.Select(id => stack[id]).ToList();
        var taskRows = await tasks.ForParentsAsync(projectId, storyIds, null, cancellationToken);
        var rollups = await tree.RollupsAsync(projectId, storyIds, null, cancellationToken);
        var byId = await cards.BuildAsync([.. parents, .. taskRows], rollups, board.Settings, cancellationToken);

        var states = TaskboardGrid.TaskStates(board);
        var taskCards = taskRows.Select(row => byId[row.Id]).ToList();
        var tasksByParent = taskCards.Where(t => t.Parent is not null).ToLookup(t => t.Parent!.Id);
        var rows = rowOrder.Select(r =>
        {
            var parent = stack[r.Story];
            var rowTasks = tasksByParent[parent.Id].ToList();
            return new FeatureBoardRowDto(byId[parent.Id], TaskboardGrid.OfferResolve(parent, rowTasks), TaskboardGrid.Cells(rowTasks, states), r.Section, parent.Sprint);
        }).ToList();

        var rowsBySection = rows.ToLookup(r => r.FeatureId);
        return new FeatureBoardViewDto(
            root.Id, board.Id, await HeaderAsync(projectId, root, layout.Count - 1, rows, taskCards, cancellationToken),
            TaskboardGrid.Columns(states, taskCards), [.. layout.Select(s => Section(s, rowsBySection[s.Id]))], rows,
            TaskboardGrid.PeopleRows(TaskboardGrid.InRowOrder(taskCards, storyIds), states), TaskboardGrid.ParentStates(board),
            board.Settings.StyleRules, board.Settings.CardFields, board.Settings.Annotations);
    }

    private async Task<FeatureBoardHeaderDto> HeaderAsync(
        Guid projectId, StackRowDto root, int featuresUnder, List<FeatureBoardRowDto> rows, List<CardDto> taskCards, CancellationToken cancellationToken)
    {
        var parents = rows.Select(r => r.Parent).ToList();
        var sprints = rows.Select(r => r.Sprint).OfType<SprintRef>().DistinctBy(s => s.Id).ToList();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var byStart = (await connection.QueryAsync<Guid>(new CommandDefinition(
            "SELECT id FROM sprint WHERE project_id = @projectId AND id = ANY(@ids) ORDER BY start_date, end_date, id",
            new { projectId, ids = sprints.Select(s => s.Id).ToArray() }, cancellationToken: cancellationToken))).ToList();
        var (storiesClosed, pointsClosed, pointsTotal) = Counts(parents);
        return new FeatureBoardHeaderDto(
            root.Id, root.Key, root.Title, root.State, featuresUnder, storiesClosed, parents.Count, pointsClosed, pointsTotal,
            taskCards.Count(t => t.State.Category == StateCategory.Completed), taskCards.Count,
            [.. byStart.Select(id => sprints.First(s => s.Id == id))], rows.Any(r => r.Sprint is null));
    }

    private static FeatureBoardSectionDto Section(FeatureBoardSection section, IEnumerable<FeatureBoardRowDto> sectionRows)
    {
        var own = sectionRows.Select(r => r.Parent).ToList();
        var (closed, pointsClosed, pointsTotal) = Counts(own);
        return new FeatureBoardSectionDto(section.Id, section.Key, section.Title, section.State, section.Depth, section.ParentFeature, closed, own.Count, pointsClosed, pointsTotal);
    }

    // Closed stories and the points of the closed and of all of them; points are null when none of the stories has any.
    private static (int Closed, decimal? PointsClosed, decimal? PointsTotal) Counts(List<CardDto> stories)
    {
        var closed = stories.Where(s => s.State.Category == StateCategory.Completed).ToList();
        var anyPoints = stories.Any(s => s.Points is not null);
        return (closed.Count, anyPoints ? closed.Sum(s => s.Points ?? 0) : null, anyPoints ? stories.Sum(s => s.Points ?? 0) : null);
    }
}
