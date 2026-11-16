using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ShipLean.Contracts.Views.InspectionMetrics;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.InspectionMetrics;

// The real facts reader, measure and aggregator on one seeded history in the empty project, through GET inspection-metrics, with
// the clock fixed on Wednesday 18 Nov 2026 (Q4). Three sprints: Sprint 1 in Q3, Sprint 2 finished and Sprint 3 running in Q4.
// T1 (Sprint 1): 10 h to its commit, resolved 14 h later. T2 (Sprint 2): 10 h to its commit, blocked 2 h, changes requested 4 h
// before the commit. T3 (Sprint 2): no commit time, resolved after 6 h. T4 (Sprint 3): still Active with a commit 4 h in (item
// D19). T5 (Sprint 3): its commit is dated 2 h before its start, so its lead time is 0 h. Every time is set in SQL. One scenario
// per class.
[TestFixture]
public class InspectionMetricsEndToEndTests : ApiFixture
{
    private static readonly Guid Empt = SeedIds.Projects.Empt;
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset S1Day = new(2026, 9, 15, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset S2Day = new(2026, 10, 27, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset S3Day = new(2026, 11, 10, 0, 0, 0, TimeSpan.Zero);

    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    private readonly FakeTimeProvider _clock = new(Now);
    private readonly Dictionary<string, Guid> _sprints = [];
    private string _t1 = string.Empty;
    private string _t2 = string.Empty;
    private string _t3 = string.Empty;
    private string _t4 = string.Empty;
    private string _t5 = string.Empty;
    private int _sha;

    protected override Action<IServiceCollection>? ConfigureTestServices => services => services.AddSingleton<TimeProvider>(_clock);

    [OneTimeSetUp]
    public async Task ArrangeHistory()
    {
        await SprintAsync("Sprint 1", "2026-09-14", "2026-09-25");
        await SprintAsync("Sprint 2", "2026-10-26", "2026-11-06");
        await SprintAsync("Sprint 3", "2026-11-09", "2026-11-20");
        var active = SeedIds.States.Empt.TaskActive;
        var resolved = SeedIds.States.Empt.TaskResolved;

        var t1 = await TaskAsync(S1Day);
        await MoveAsync(t1.Id, 2, S1Day.AddHours(9), active);
        await CommitAsync(t1.Id, S1Day.AddHours(19));
        await MoveAsync(t1.Id, 3, S1Day.AddHours(33), resolved);

        var t2 = await TaskAsync(S2Day);
        await MoveAsync(t2.Id, 2, S2Day.AddHours(9), active);
        await TagChangeAsync(t2.Id, 3, S2Day.AddHours(11), "[]", """["blocked"]""");
        await TagChangeAsync(t2.Id, 4, S2Day.AddHours(13), """["blocked"]""", "[]");
        await ReviewAsync(t2.Id, S2Day.AddHours(15), "changesRequested");
        await CommitAsync(t2.Id, S2Day.AddHours(19));
        await MoveAsync(t2.Id, 5, S2Day.AddHours(33), resolved);
        await ReviewAsync(t2.Id, S2Day.AddHours(32), "approved");

        var t3 = await TaskAsync(S2Day);
        await MoveAsync(t3.Id, 2, S2Day.AddHours(33), active);
        await MoveAsync(t3.Id, 3, S2Day.AddHours(39), resolved);

        var t4 = await TaskAsync(S3Day);
        await MoveAsync(t4.Id, 2, S3Day.AddHours(9), active);
        await CommitAsync(t4.Id, S3Day.AddHours(13));

        var t5 = await TaskAsync(S3Day);
        await MoveAsync(t5.Id, 2, S3Day.AddHours(33), active);
        await CommitAsync(t5.Id, S3Day.AddHours(31));
        await MoveAsync(t5.Id, 3, S3Day.AddHours(35), resolved);

        (_t1, _t2, _t3, _t4, _t5) = (t1.Key, t2.Key, t3.Key, t4.Key, t5.Key);
    }

    // Mutation: in TaskLeadTimeMeasure, end at the first resolve instead of the last commit (T1 and T2 read 24 h, Sprint 2 15 h).
    [Test]
    public async Task Get_WhenSeededHistory_ShouldReturnExpectedMeansAndQuarter()
    {
        var view = await GetAsync(string.Empty);

        view.Periods.Select(p => (p.Name, p.Quarter, p.InProgress)).ShouldBe(
            [("Sprint 1", "Q3 2026", false), ("Sprint 2", "Q4 2026", false), ("Sprint 3", "Q4 2026", true)]);
        var s1 = view.Periods[0].LeadTime;
        var s2 = view.Periods[1].LeadTime;
        var s3 = view.Periods[2].LeadTime;
        (s1.Tasks, s1.MeanHours, s1.EndedAtResolved).ShouldBe((1, 10d, 0));
        (s2.Tasks, s2.MeanHours, s2.EndedAtResolved, s2.ChangePctVsPrevious).ShouldBe((2, 8d, 1, -20));
        (s2.MeanActiveHours, s2.MeanBlockedHours, s2.MeanReworkHours).ShouldBe((5d, 1d, 2d));
        (s3.Tasks, s3.MeanHours, s3.EndedAtResolved).ShouldBe((2, 2d, 0));
        view.Periods[1].Dots.Select(d => (d.Key, d.LeadHours, d.EndedBy)).ShouldBe(
            [(_t2, 10d, LeadTimeEndedBy.LastCommit), (_t3, 6d, LeadTimeEndedBy.Resolved)], ignoreOrder: true);
        view.Periods[2].Dots.Select(d => d.Key).ShouldBe([_t4, _t5], ignoreOrder: true);

        view.Headline.Finished.ShouldNotBeNull().Name.ShouldBe("Sprint 2");
        view.Headline.Finished.LeadTime.MeanHours.ShouldBe(8d);
        view.Headline.SoFar.ShouldNotBeNull().Name.ShouldBe("Sprint 3");
        view.Headline.SoFar.LeadTime.MeanHours.ShouldBe(2d);
        var toDate = view.Headline.QuarterToDate.ShouldNotBeNull();
        (toDate.Quarter, toDate.ToDate, toDate.LeadTime.Tasks, toDate.LeadTime.MeanHours).ShouldBe(("Q4 2026", true, 4, 5d));
        toDate.PeriodIds.ShouldBe([_sprints["Sprint 2"], _sprints["Sprint 3"]], ignoreOrder: true);
        var last = view.Headline.LastQuarter.ShouldNotBeNull();
        (last.Quarter, last.ToDate, last.LeadTime.Tasks, last.LeadTime.MeanHours).ShouldBe(("Q3 2026", false, 1, 10d));
        view.Quarters.Select(q => (q.Quarter, q.LeadTime.MeanHours)).ShouldBe([("Q3 2026", 10d), ("Q4 2026", 5d)]);

        (view.Coverage.Tasks, view.Coverage.WithCommitTime, view.Coverage.Percent).ShouldBe((5, 4, 80));
        view.LeftOut.ShouldBeEmpty();
    }

    // The opened sprint's task: lead time ends at the last commit, and the Approved review and the Resolved move after it are listed
    // up to the end of the unresolved tail but not counted.
    [Test]
    public async Task Get_WhenTaskCommittedThenResolvedLater_ShouldEndAtLastCommitWithTail()
    {
        var view = await GetAsync($"?period={_sprints["Sprint 2"]}&select={_t2}");

        var selected = view.PeriodDetail.ShouldNotBeNull().Selected.ShouldNotBeNull();
        var task = selected.Task;
        (task.StartedAt, task.EndedAt, task.EndedBy).ShouldBe((S2Day.AddHours(9), S2Day.AddHours(19), LeadTimeEndedBy.LastCommit));
        (task.LeadHours, task.ActiveHours, task.BlockedHours, task.ReworkHours, task.UnresolvedTailHours).ShouldBe((10d, 4d, 2d, 4d, 14d));
        (task.Reviews, task.Commits).ShouldBe((2, 1));
        selected.Segments.Select(s => s.Kind).ShouldBe(
            [InspectionSegmentKind.Active, InspectionSegmentKind.Blocked, InspectionSegmentKind.Active, InspectionSegmentKind.Rework, InspectionSegmentKind.UnresolvedTail]);
    }

    // Known edge (B2/B3): a commit dated before the start. The lead time is clamped at 0 h, and no part of it may read negative or
    // more than the lead time itself.
    [Test]
    public async Task Get_WhenCommitBeforeStart_ShouldShowZeroLeadWithNoParts()
    {
        var view = await GetAsync($"?period={_sprints["Sprint 3"]}&select={_t5}");

        var task = view.PeriodDetail.ShouldNotBeNull().Selected.ShouldNotBeNull().Task;
        (task.Key, task.LeadHours, task.EndedBy).ShouldBe((_t5, 0d, LeadTimeEndedBy.LastCommit));
        (task.ActiveHours, task.BlockedHours, task.ReworkHours).ShouldBe((0d, 0d, 0d));
    }

    private async Task<InspectionMetricsViewDto> GetAsync(string query)
    {
        using var response = await Api.GetAsync($"api/projects/{Empt}/inspection-metrics{query}");
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonSerializer.Deserialize<InspectionMetricsViewDto>(text, Wire)!;
    }

    private async Task SprintAsync(string name, string start, string end)
    {
        var id = Guid.NewGuid();
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync(
            "INSERT INTO sprint (id, project_id, name, start_date, end_date) VALUES (@id, @Empt, @name, CAST(@start AS date), CAST(@end AS date))",
            new { id, Empt, name, start, end });
        _sprints[name] = id;
    }

    private async Task<(Guid Id, string Key)> TaskAsync(DateTimeOffset day)
    {
        await using var connection = await OpenDatabaseAsync();
        var id = await new WorkItemBuilder(Empt).WithType("task").WithCreatedAt(day).WithCreatedBy(SeedIds.Users.Joel).InsertAsync(connection);
        await connection.ExecuteAsync("UPDATE revision SET changed_at = @day WHERE work_item_id = @id AND rev = 1", new { id, day });
        return (id, await connection.ExecuteScalarAsync<string>("SELECT key FROM work_item WHERE id = @id", new { id }) ?? throw new InvalidOperationException());
    }

    private async Task MoveAsync(Guid id, int rev, DateTimeOffset at, Guid state)
    {
        await using var connection = await OpenDatabaseAsync();
        await new RevisionBuilder(id).WithRev(rev).WithChangedAt(at).WithChangedBy(SeedIds.Users.Joel)
            .WithChanges($$"""[{"field":"state","from":null,"to":"{{state}}"}]""").InsertAsync(connection);
        await connection.ExecuteAsync("UPDATE work_item SET state_id = @state, revision = @rev WHERE id = @id", new { id, state, rev });
    }

    private async Task TagChangeAsync(Guid id, int rev, DateTimeOffset at, string from, string to)
    {
        await using var connection = await OpenDatabaseAsync();
        await new RevisionBuilder(id).WithRev(rev).WithChangedAt(at).WithChangedBy(SeedIds.Users.Joel)
            .WithChanges($$"""[{"field":"tags","from":{{from}},"to":{{to}}}]""").InsertAsync(connection);
    }

    private async Task ReviewAsync(Guid id, DateTimeOffset at, string verdict)
    {
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync(
            "INSERT INTO review_event (id, work_item_id, verdict, note, by_user_id, agent_name, at) VALUES (@eventId, @id, @verdict, NULL, @by, 'reviewer', @at)",
            new { eventId = Guid.NewGuid(), id, verdict, by = SeedIds.Users.Joel, at });
    }

    // A reported commit of a repository ShipLean can't read: a url link titled "commit <sha>" with its time (item D12).
    private async Task CommitAsync(Guid id, DateTimeOffset at)
    {
        var sha = $"{++_sha:x7}e1e1e1";
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync(
            "INSERT INTO work_item_link (id, source_id, link_type, title, url, committed_at) VALUES (@linkId, @id, 'url', @title, @url, @at)",
            new { linkId = Guid.NewGuid(), id, title = $"commit {sha}", url = $"https://git.example.test/acme/empt/commit/{sha}", at });
    }
}
