using ShipLean.Application.Features.Backlog.Shared;
using Dapper;
using Npgsql;
using ShipLean.Application.Sprints;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Sprints;
using ShipLean.Contracts.Views;
using ShipLean.Contracts.WorkItems.Reads.Tree;
using ShipLean.Core;

namespace ShipLean.Application.Features.Backlog.Stories;

// Planning mode: the current sprint, each future sprint (by start date), then the Backlog. Every section is the same stack
// filtered by sprint, and rank restarts in each. Past sprints have no section: their items do not appear. A sprint's Points are
// what is committed to it: every live story and bug in the sprint, completed included, whatever the filter and includeCompleted
// say. The Backlog section's Points are the sum of its visible rows.
public sealed class BacklogPlanning(NpgsqlDataSource dataSource, ISprintStore sprints, TimeProvider clock)
{
    public const string BacklogSectionId = "backlog";

    public async Task<IReadOnlyList<BacklogSectionDto>> BuildAsync(
        Guid projectId, IReadOnlyList<StackRowDto> stories, IReadOnlyList<StackRowDto> committed, BacklogRows factory, CancellationToken cancellationToken)
    {
        var today = SprintLabels.Today(clock);
        var live = new List<SprintRef>();
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        {
            var rows = await connection.QueryAsync<SprintListRow>(new CommandDefinition(
                """
                SELECT id, name, to_char(start_date, 'YYYY-MM-DD') AS startDate, to_char(end_date, 'YYYY-MM-DD') AS endDate
                FROM sprint WHERE project_id = @projectId AND archived_at IS NULL ORDER BY start_date, name
                """,
                new { projectId }, cancellationToken: cancellationToken));
            live.AddRange(rows.Select(row => row.ToRef(today)).Where(sprint => sprint.Status != SprintStatus.Past));
        }

        var sections = new List<BacklogSectionDto>();
        foreach (var sprint in live.OrderBy(s => s.Status))
        {
            var capacity = await sprints.GetCapacityAsync(sprint.Id, cancellationToken);
            sections.Add(Section(sprint.Id.ToString(), sprint.Name, sprint, capacity.Members.Count == 0 ? null : capacity.TeamTotal, stories.Where(s => s.Sprint?.Id == sprint.Id), CommittedPoints(committed, sprint.Id), factory));
        }
        sections.Add(Section(BacklogSectionId, "Backlog", null, null, stories.Where(s => s.Sprint is null), null, factory));
        return sections;
    }

    private static decimal CommittedPoints(IEnumerable<StackRowDto> committed, Guid sprintId) =>
        committed.Where(s => s.Sprint?.Id == sprintId).Sum(s => s.Points ?? 0);

    private static BacklogSectionDto Section(string id, string title, SprintRef? sprint, decimal? capacity, IEnumerable<StackRowDto> members, decimal? points, BacklogRows factory)
    {
        var items = members.ToList();
        return new BacklogSectionDto(id, title, sprint, points ?? items.Sum(s => s.Points ?? 0), capacity, [.. items.Select((s, index) => factory.Item(s, 0, index + 1))]);
    }
}
