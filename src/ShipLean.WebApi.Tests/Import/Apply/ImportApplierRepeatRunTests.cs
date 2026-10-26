using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Fakes.Import;
using ShipLean.Application.Features.Import.Apply;
using ShipLean.Contracts.Boards;
using ShipLean.Contracts.Comments;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Import;
using ShipLean.Contracts.Import.Jira;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Projects;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Sprints;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.Import.Mapping;

namespace ShipLean.WebApi.Tests.Import.Apply;

// What the applier does when a run fails half way or a run follows another: a state whose column add failed, sprints that touch, parents written by an earlier run,
// a user's own board column, one bad comment author, a sprint made by someone else at the same moment. Each test uses names of its own: the project is shared in the class.
public sealed class ImportApplierRepeatRunTests : ApiFixture
{
    private static readonly Guid Connection = Guid.NewGuid();
    private static readonly Guid Job = Guid.NewGuid();
    private static readonly JiraUser Joel = JiraWorld.Person("acct-joel", "Joel Dickson", "joel@shiplean.local");
    private static readonly JiraUser Bad = JiraWorld.Person("acct-bad", "Bad Email", "not-an-email");
    private static readonly JiraIssue Epic = JiraWorld.Issue("PLAT-10", "Epic", reporter: Joel);
    private static readonly JiraIssue Story = JiraWorld.Issue("PLAT-11", status: "In Review", reporter: Joel, parent: "PLAT-10");
    private static readonly JiraIssue Related = JiraWorld.Issue("PLAT-12", reporter: Joel, links: [new JiraIssueLink("Relates", JiraLinkDirection.Outward, "PLAT-11")]);
    private static readonly RecordingCommentStore CommentStore = new();

    private static bool _failBoardPut;
    private static bool _racedSprintCreate;

    private ImportMappingDto _mapping = null!;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        var comments = new Dictionary<string, IReadOnlyList<JiraComment>>
        {
            ["PLAT-11"] = [new JiraComment("c1", Bad, "From a bad address", JiraWorld.Day, JiraWorld.Day), new JiraComment("c2", Joel, "From Joel", JiraWorld.Day, JiraWorld.Day)],
        };
        var statuses = new[]
        {
            JiraWorld.Status("To Do", JiraStatusCategory.New), JiraWorld.Status("In Progress", JiraStatusCategory.Indeterminate),
            JiraWorld.Status("In Review", JiraStatusCategory.Indeterminate, "Story"), JiraWorld.Status("Done", JiraStatusCategory.Done),
        };
        var connections = new InMemoryJiraConnections();
        connections.Arrange(Connection, JiraWorld.Credentials);
        services.AddSingleton<IJiraClient>(new InMemoryJiraClient(JiraWorld.Credentials, JiraWorld.Data([Epic, Story, Related], statuses, comments: comments)));
        services.AddSingleton<IJiraConnections>(connections);
        services.AddSingleton<ICommentStore>(CommentStore);
        Decorate<IBoardSettingsStore>(services, (method, _, _) => _failBoardPut && method.Name == nameof(IBoardSettingsStore.PutAsync)
            ? Task.FromException<BoardSettingsPutResult>(ApiException.Validation("The board save failed."))
            : null);
        Decorate<ISprintStore>(services, (method, args, inner) => _racedSprintCreate && method.Name == nameof(ISprintStore.CreateAsync) ? CreateThenConflictAsync(inner, args!) : null);
    };

    [OneTimeSetUp]
    public async Task ArrangeJobAndMapping()
    {
        await using var connection = await OpenDatabaseAsync();
        var jiraConnectionId = Guid.NewGuid();
        await connection.ExecuteAsync(
            "INSERT INTO jira_connection (id, project_id, base_url, email) VALUES (@jiraConnectionId, @project, 'https://acme.example.test', 'priya@acme.io')",
            new { jiraConnectionId, project = SeedIds.Projects.Empt });
        await connection.ExecuteAsync(
            "INSERT INTO import_job (id, connection_id, jira_project_key, project_key, project_id, status, progress) VALUES (@Job, @jiraConnectionId, 'PLAT', 'EMPT', @project, 'running', '{}'::jsonb)",
            new { Job, jiraConnectionId, project = SeedIds.Projects.Empt });
        await using var scope = Host.Services.CreateAsyncScope();
        _mapping = await scope.ServiceProvider.GetRequiredService<IImportMappings>().SuggestAsync(Connection, JiraWorld.ProjectKey, null);
    }

    [Test]
    public async Task Apply_WhenColumnAddFailedBefore_ShouldAddTheColumnOnTheNextRun()
    {
        var mapping = WithStoryState("Review A");
        _failBoardPut = true;
        try
        {
            await WithApplierAsync(mapping, async (applier, run, _) => await Should.ThrowAsync<ApiException>(() => applier.PrepareAsync(run, [])));
        }
        finally
        {
            _failBoardPut = false;
        }

        (await StoryColumnsAsync()).ShouldNotContain(c => c.Name == "Review A");

        var stateId = await WithApplierAsync(mapping, async (applier, run, services) =>
        {
            await applier.PrepareAsync(run, []);
            return (await services.GetRequiredService<IStateCatalog>().ListAsync(SeedIds.Projects.Empt)).Single(s => s.Type == WorkItemTypeId.Story && s.Name == "Review A").Id;
        });

        (await StoryColumnsAsync()).Where(c => c.StateMappings.Any(m => m.Type == WorkItemTypeId.Story && m.StateId == stateId)).Select(c => c.Name).ShouldBe(["Review A"]);
    }

    [Test]
    public async Task Apply_WhenColumnNamedLikeTheNewStateMapsAnotherState_ShouldLeaveItsMappingAlone()
    {
        var board = await StoryBoardAsync();
        var active = board.Settings.Columns.Single(c => c.Name == "Active");
        var renamed = board.Settings.Columns.Select(c => c == active ? c with { Name = "Review D" } : c).ToList();
        await WithApplierAsync(_mapping, async (_, _, services) =>
        {
            await services.GetRequiredService<IBoardSettingsStore>().PutAsync(board.Id, new BoardSettingsPut(board.Settings, board.Settings with { Columns = renamed }));
            return 0;
        });

        var stateId = await WithApplierAsync(WithStoryState("Review D"), async (applier, run, services) =>
        {
            await applier.PrepareAsync(run, []);
            return (await services.GetRequiredService<IStateCatalog>().ListAsync(SeedIds.Projects.Empt)).Single(s => s.Type == WorkItemTypeId.Story && s.Name == "Review D").Id;
        });

        var columns = (await StoryBoardAsync()).Settings.Columns;
        columns.Single(c => c.Id == active.Id).StateMappings.ShouldBe(active.StateMappings);
        columns.Count(c => c.StateMappings.Any(m => m.Type == WorkItemTypeId.Story && m.StateId == stateId)).ShouldBe(1);
        columns.Single(c => c.StateMappings.Any(m => m.Type == WorkItemTypeId.Story && m.StateId == stateId)).Id.ShouldNotBe(active.Id);
    }

    [Test]
    public async Task Apply_WhenSprintsHandOverOnTheSameDayOrRunInParallel_ShouldStartTheLaterOnesAfterTheEarlier()
    {
        var first = new JiraSprint(201, "Hand A", "closed", JiraWorld.Day, JiraWorld.Day.AddDays(13), null);
        var second = new JiraSprint(202, "Hand B", "closed", JiraWorld.Day.AddDays(13), JiraWorld.Day.AddDays(20), null);
        var parallel = new JiraSprint(203, "Hand C", "closed", JiraWorld.Day.AddDays(15), JiraWorld.Day.AddDays(18), null);

        var warnings = await WithApplierAsync(_mapping, async (applier, run, _) =>
        {
            await applier.PrepareAsync(run, [parallel, second, first]);
            return run.Warnings;
        });

        var dates = await SprintDatesAsync("Hand A", "Hand B", "Hand C");
        dates["Hand A"].ShouldBe((DateOnly.FromDateTime(JiraWorld.Day.UtcDateTime), DateOnly.FromDateTime(JiraWorld.Day.AddDays(13).UtcDateTime)));
        dates["Hand B"].ShouldBe((DateOnly.FromDateTime(JiraWorld.Day.AddDays(14).UtcDateTime), DateOnly.FromDateTime(JiraWorld.Day.AddDays(21).UtcDateTime)));
        dates["Hand C"].ShouldBe((DateOnly.FromDateTime(JiraWorld.Day.AddDays(22).UtcDateTime), DateOnly.FromDateTime(JiraWorld.Day.AddDays(25).UtcDateTime)));
        warnings.Count.ShouldBe(2);
        warnings[0].ShouldContain("Hand B");
    }

    [Test]
    public async Task Apply_WhenFreshRunAppliesChildFirst_ShouldKeepParent()
    {
        var storyId = await WithApplierAsync(_mapping, async (applier, run, _) =>
        {
            await applier.PrepareAsync(run, []);
            await applier.ApplyIssueAsync(run, Epic);
            return (await applier.ApplyIssueAsync(run, Story)).ItemId;
        });

        var again = await WithApplierAsync(_mapping, async (applier, run, services) =>
        {
            await applier.PrepareAsync(run, []);
            var outcome = await applier.ApplyIssueAsync(run, Story);
            return (outcome, await services.GetRequiredService<IWorkItemStore>().GetAsync(outcome.ItemId!.Value));
        });

        again.outcome.UnresolvedKeys.ShouldBeEmpty();
        again.Item2.ParentId.ShouldNotBeNull();
        again.Item2.Id.ShouldBe(storyId.GetValueOrDefault());
    }

    [Test]
    public async Task Apply_WhenOneIssueRetried_ShouldKeepItsParentAndLinks()
    {
        var chain = await WithApplierAsync(_mapping, async (applier, run, _) =>
        {
            await applier.PrepareAsync(run, []);
            await applier.ApplyIssueAsync(run, Epic);
            var story = await applier.ApplyIssueAsync(run, Story);
            await applier.ApplyIssueAsync(run, Related);
            return story.ItemId!.Value;
        });

        var retried = await WithApplierAsync(_mapping, async (applier, run, services) =>
        {
            await applier.PrepareAsync(run, []);
            var outcome = await applier.ApplyIssueAsync(run, Related);
            return (outcome, await services.GetRequiredService<IWorkItemStore>().GetAsync(outcome.ItemId!.Value));
        });

        retried.outcome.UnresolvedKeys.ShouldBeEmpty();
        retried.Item2.Links.Select(l => (l.Value.Type, l.Value.WorkItemId)).ShouldBe([(LinkType.Related, (Guid?)chain)]);
    }

    [Test]
    public async Task Apply_WhenCommentAuthorIsInvalid_ShouldWriteOneImportErrorAndContinue()
    {
        var itemId = await WithApplierAsync(_mapping, async (applier, run, _) =>
        {
            await applier.PrepareAsync(run, []);
            return (await applier.ApplyIssueAsync(run, Story)).ItemId!.Value;
        });
        var mapping = _mapping with { Users = [.. _mapping.Users, new UserMappingDto(Bad.AccountId, Bad.DisplayName, Bad.Email, 1, UserResolution.Create, null, false, false)] };

        var written = await WithApplierAsync(mapping, (applier, run, _) => applier.ApplyCommentsAsync(run, Story, itemId));

        written.ShouldBe(1);
        CommentStore.Imported.Where(c => c.WorkItemId == itemId).Select(c => c.Comment.OriginalSystemId).ShouldBe(["c2"]);
        await using var connection = await OpenDatabaseAsync();
        (await connection.QuerySingleAsync<string>("SELECT source_key FROM import_error WHERE import_job_id = @Job AND entity = 'comment'", new { Job })).ShouldBe("PLAT-11/c1");
    }

    [Test]
    public async Task Apply_WhenSprintCreatedByAnotherWriterFirst_ShouldUseThatSprint()
    {
        var raced = new JiraSprint(301, "Raced Sprint", "future", JiraWorld.Day.AddDays(100), JiraWorld.Day.AddDays(110), null);
        _racedSprintCreate = true;
        try
        {
            await WithApplierAsync(_mapping, async (applier, run, _) =>
            {
                await applier.PrepareAsync(run, [raced]);
                return 0;
            });
        }
        finally
        {
            _racedSprintCreate = false;
        }

        await using var connection = await OpenDatabaseAsync();
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM sprint WHERE project_id = @p AND name = 'Raced Sprint'", new { p = SeedIds.Projects.Empt })).ShouldBe(1);
    }

    private ImportMappingDto WithStoryState(string name)
        => _mapping with { Statuses = [.. _mapping.Statuses.Select(s => s.JiraType == "Story" && s.JiraStatus == "In Review" ? s with { TargetState = name } : s)] };

    private async Task<T> WithApplierAsync<T>(ImportMappingDto mapping, Func<IImportApplier, ImportRun, IServiceProvider, Task<T>> act)
    {
        await using var scope = Host.Services.CreateAsyncScope();
        var applier = scope.ServiceProvider.GetRequiredService<IImportApplier>();
        return await act(applier, applier.Begin(SeedIds.Projects.Empt, Job, JiraWorld.Credentials, mapping), scope.ServiceProvider);
    }

    private async Task<(Guid Id, BoardSettingsJson Settings)> StoryBoardAsync()
    {
        await using var scope = Host.Services.CreateAsyncScope();
        var boards = scope.ServiceProvider.GetRequiredService<IBoardSettingsStore>();
        var summary = (await boards.ListAsync(SeedIds.Projects.Empt)).Single(b => b.Level == BoardLevel.Stories);
        return (summary.Id, (await boards.GetAsync(summary.Id)).Settings);
    }

    private async Task<IReadOnlyList<BoardColumnJson>> StoryColumnsAsync() => (await StoryBoardAsync()).Settings.Columns;

    private async Task<Dictionary<string, (DateOnly Start, DateOnly End)>> SprintDatesAsync(params string[] names)
    {
        await using var connection = await OpenDatabaseAsync();
        var rows = await connection.QueryAsync<(string Name, DateOnly Start, DateOnly End)>(
            "SELECT name AS Name, start_date AS Start, end_date AS End FROM sprint WHERE project_id = @p AND name = ANY(@names) AND archived_at IS NULL",
            new { p = SeedIds.Projects.Empt, names });
        return rows.ToDictionary(r => r.Name, r => (r.Start, r.End));
    }

    private static void Decorate<T>(IServiceCollection services, Func<System.Reflection.MethodInfo, object?[]?, T, object?> fault)
        where T : class
    {
        var original = services.Single(d => d.ServiceType == typeof(T));
        services.Remove(original);
        services.Add(new ServiceDescriptor(
            typeof(T),
            sp => FaultProxy<T>.Wrap((T)ActivatorUtilities.CreateInstance(sp, original.ImplementationType!), fault),
            original.Lifetime));
    }

    // Another writer's create of the same sprint won: the sprint exists and this create is refused.
    private static async Task<SprintDto> CreateThenConflictAsync(ISprintStore inner, object?[] args)
    {
        await inner.CreateAsync((Guid)args[0]!, (SprintCreate)args[1]!, (System.Data.Common.DbTransaction?)args[2], (CancellationToken)args[3]!);
        throw ApiException.Conflict("A sprint with that name exists.");
    }
}
