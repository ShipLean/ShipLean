using ShipLean.Application.Features.Backlog.Shared;
using ShipLean.Contracts.Backlog;
using ShipLean.Contracts.Boards;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Sprints;
using ShipLean.Contracts.Views;
using ShipLean.Contracts.WorkItems.Reads.Tree;

namespace ShipLean.Application.Features.Backlog.Stories;

// The Stories backlog over the tree reads: one stack read of the Requirement level carries the filter and includeCompleted, and
// tree mode adds the group levels. Tag colours are the Stories board's.
public sealed class StoriesBacklogReads(
    IWorkTreeReads reads, IBoardSettingsStore boards, ISprintMetrics metrics, BacklogPlanning planning) : IStoriesBacklogReads
{
    public async Task<BacklogViewDto> GetAsync(Guid projectId, BacklogMode mode, string? filter, bool includeCompleted, CancellationToken cancellationToken = default)
    {
        var criteria = BacklogFilter.Parse(filter);
        var read = await reads.StackAsync(projectId, StackLevel.Requirement, criteria, includeCompleted, cancellationToken);
        var rank = await RanksAsync(projectId, criteria, includeCompleted, read, cancellationToken);
        // A story that closed or went between the two reads has no rank in the whole read: it is left out.
        var stories = read.Where(s => rank.ContainsKey(s.Id)).ToList();
        var factory = new BacklogRows(await TagColoursAsync(projectId, cancellationToken));

        switch (mode)
        {
            case BacklogMode.Tree:
                var withClosed = includeCompleted
                    ? stories
                    : await reads.StackAsync(projectId, StackLevel.Requirement, criteria, true, cancellationToken);
                var tree = await BacklogTree.BuildAsync(reads, projectId, new TreeStories(stories, withClosed, rank), criteria, factory, cancellationToken);
                return View(projectId, mode, stories.Count, tree, [], factory, null, null);
            case BacklogMode.Planning:
                var committed = await reads.StackAsync(projectId, StackLevel.Requirement, null, true, cancellationToken);
                var sections = await planning.BuildAsync(projectId, stories, committed, factory, cancellationToken);
                var forecast = await metrics.GetForecastAsync(projectId, cancellationToken);
                var velocity = await metrics.GetVelocityAsync(projectId, cancellationToken);
                return View(projectId, mode, sections.Sum(s => s.Rows.Count), [], sections, factory, forecast, velocity);
            default:
                return View(projectId, mode, stories.Count, [.. stories.Select(s => factory.Item(s, 0, rank[s.Id]))], [], factory, null, null);
        }
    }

    // A row's rank is its place in the whole backlog (honouring only includeCompleted), so a filter never renumbers it.
    private async Task<Dictionary<Guid, int>> RanksAsync(
        Guid projectId, CriteriaJson? criteria, bool includeCompleted, IReadOnlyList<StackRowDto> stories, CancellationToken cancellationToken)
    {
        var whole = criteria is null ? stories : await reads.StackAsync(projectId, StackLevel.Requirement, null, includeCompleted, cancellationToken);
        return whole.Select((story, index) => (story.Id, Rank: index + 1)).ToDictionary(x => x.Id, x => x.Rank);
    }

    private static BacklogViewDto View(
        Guid projectId, BacklogMode mode, int total, IReadOnlyList<BacklogRowDto> rows, IReadOnlyList<BacklogSectionDto> sections,
        BacklogRows factory, ForecastDto? forecast, VelocityDto? velocity)
        => new(projectId, BoardLevel.Stories, mode, total, rows, sections, factory.TagColours, forecast, velocity);

    private async Task<IReadOnlyList<TagColourJson>> TagColoursAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var board = (await boards.ListAsync(projectId, cancellationToken)).First(b => b.Level == BoardLevel.Stories);
        return (await boards.GetAsync(board.Id, cancellationToken)).Settings.TagColours;
    }
}
