using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Fakes.Import;
using ShipLean.Application.Features.Import.Apply;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Import;
using ShipLean.Contracts.Import.Jira;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Projects;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Comments;
using ShipLean.Contracts.Users;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.Import.Mapping;

namespace ShipLean.WebApi.Tests.Import.Apply;

// Applying Jira issues to the seeded empty project EMPT with the real stores; the Jira client, its connections and the comment and attachment stores are pinned doubles.
public sealed class ImportApplierTests : ApiFixture
{
    private static readonly Guid Connection = Guid.NewGuid();
    private static readonly Guid Job = Guid.NewGuid();
    private static readonly JiraUser Joel = JiraWorld.Person("acct-joel", "Joel Dickson", "joel@shiplean.local");
    private static readonly JiraUser Nina = JiraWorld.Person("acct-nina", "Nina New", "nina@acme.io");
    private static readonly JiraUser Ghost = JiraWorld.Person("acct-ghost", "Ghost", null);
    private static readonly JiraSprint Sprint1 = new(101, "Sprint 1", "closed", JiraWorld.Day, JiraWorld.Day.AddDays(13), "Ship the thing");
    private static readonly JiraSprint Sprint2 = new(102, "Sprint 2", "future", null, null, null);
    private static readonly JiraAttachment Spec = new("att-1", "spec.txt", "text/plain", 5, "https://acme.example.test/secure/attachment/att-1/spec.txt");
    private static readonly RecordingCommentStore CommentStore = new();
    private static readonly RecordingAttachmentStore AttachmentStore = new();

    private static readonly JiraIssue Epic = JiraWorld.Issue("PLAT-10", "Epic", reporter: Joel);
    private static readonly JiraIssue Story = JiraWorld.Issue(
        "PLAT-11", status: "In Review", assignee: Nina, reporter: Joel, parent: "PLAT-10", points: 5, sprints: [Sprint1], priority: "High",
        attachments: [Spec], changelog:
        [
            new JiraChangelogEntry("1", Joel, JiraWorld.Day.AddDays(1), [new JiraChangeItem("status", "To Do", "In Review"), new JiraChangeItem("Sprint", null, "Sprint 1")]),
            new JiraChangelogEntry("2", Nina, JiraWorld.Day.AddDays(2), [new JiraChangeItem("summary", "Old title", "Summary of PLAT-11")]),
        ]);
    private static readonly JiraIssue Planned = JiraWorld.Issue("PLAT-12", sprints: [Sprint2], links: [new JiraIssueLink("Relates", JiraLinkDirection.Outward, "PLAT-11")]);
    private static readonly JiraIssue LateParent = JiraWorld.Issue("PLAT-20", reporter: Joel);
    private static readonly JiraIssue EarlyChild = JiraWorld.Issue("PLAT-21", "Sub-task", parent: "PLAT-20", assignee: Ghost);
    private static readonly JiraIssue Sub = JiraWorld.Issue("PLAT-13", "Sub-task", parent: "PLAT-11", assignee: Ghost);

    private ImportMappingDto _mapping = null!;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        var comments = new Dictionary<string, IReadOnlyList<JiraComment>>
        {
            ["PLAT-11"] =
            [
                new JiraComment("c1", Joel, "First", JiraWorld.Day, JiraWorld.Day),
                new JiraComment("c2", Nina, "Second", JiraWorld.Day.AddDays(1), JiraWorld.Day.AddDays(1)),
                new JiraComment("c3", Ghost, "Third", JiraWorld.Day.AddDays(2), JiraWorld.Day.AddDays(2)),
            ],
        };
        var statuses = new[]
        {
            JiraWorld.Status("To Do", JiraStatusCategory.New), JiraWorld.Status("In Progress", JiraStatusCategory.Indeterminate),
            JiraWorld.Status("In Review", JiraStatusCategory.Indeterminate, "Story"), JiraWorld.Status("Done", JiraStatusCategory.Done),
        };
        var data = JiraWorld.Data([Epic, Story, Planned, Sub], statuses, comments: comments, attachmentBytes: new Dictionary<string, byte[]> { ["att-1"] = Encoding.UTF8.GetBytes("hello") });
        var connections = new InMemoryJiraConnections();
        connections.Arrange(Connection, JiraWorld.Credentials);
        services.AddSingleton<IJiraClient>(new InMemoryJiraClient(JiraWorld.Credentials, data));
        services.AddSingleton<IJiraConnections>(connections);
        services.AddSingleton<ICommentStore>(CommentStore);
        services.AddSingleton<IAttachmentStore>(AttachmentStore);
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

    private async Task<T> WithApplierAsync<T>(ImportMappingDto mapping, Func<IImportApplier, ImportRun, IServiceProvider, Task<T>> act)
    {
        await using var scope = Host.Services.CreateAsyncScope();
        var applier = scope.ServiceProvider.GetRequiredService<IImportApplier>();
        return await act(applier, applier.Begin(SeedIds.Projects.Empt, Job, JiraWorld.Credentials, mapping), scope.ServiceProvider);
    }

    private Task<T> WithApplierAsync<T>(Func<IImportApplier, ImportRun, IServiceProvider, Task<T>> act) => WithApplierAsync(_mapping, act);

    private async Task<WorkItemSnapshot> ApplyStoryChainAsync()
    {
        return await WithApplierAsync(async (applier, run, services) =>
        {
            await applier.PrepareAsync(run, [Sprint1, Sprint2]);
            await applier.ApplyIssueAsync(run, Epic);
            var outcome = await applier.ApplyIssueAsync(run, Story);
            return await services.GetRequiredService<IWorkItemStore>().GetAsync(outcome.ItemId!.Value);
        });
    }

    [Test]
    [Order(1)]
    public async Task Apply_WhenStatusUnmapped_ShouldCreateStateWithJiraCategoryAndBoardColumn()
    {
        _mapping.Statuses.Single(s => s.JiraType == "Story" && s.JiraStatus == "In Review").CreatesState.ShouldBeTrue();

        var created = await WithApplierAsync(async (applier, run, services) =>
        {
            await applier.PrepareAsync(run, []);
            var states = await services.GetRequiredService<IStateCatalog>().ListAsync(SeedIds.Projects.Empt);
            return states.Single(s => s.Type == WorkItemTypeId.Story && s.Name == "In Review");
        });

        created.Category.ShouldBe(StateCategory.InProgress);
        await using var connection = await OpenDatabaseAsync();
        var settings = await connection.QuerySingleAsync<string>("SELECT settings::text FROM board WHERE project_id = @p AND level = 'stories'", new { p = SeedIds.Projects.Empt });
        var column = JsonDocument.Parse(settings).RootElement.GetProperty("columns").EnumerateArray().Single(c => c.GetProperty("name").GetString() == "In Review");
        column.GetProperty("stateMappings").EnumerateArray().Single(m => m.GetProperty("type").GetString() == "story").GetProperty("stateId").GetGuid().ShouldBe(created.Id);
    }

    [Test]
    [Order(2)]
    public async Task Apply_WhenIssuesApplied_ShouldKeepJiraNumbersAndOriginalFields()
    {
        var story = await ApplyStoryChainAsync();

        story.Number.ShouldBe(11);
        story.Type.ShouldBe(WorkItemTypeId.Story);
        story.OriginalSystem.ShouldBe(new OriginalSystemDto("jira", "PLAT-11", "https://acme.example.test/browse/PLAT-11"));
        story.Points.ShouldBe(5);
        story.Priority.ShouldBe(2);
        story.Title.ShouldBe("Summary of PLAT-11");
        story.CreatedBy.ShouldBe(SeedIds.Users.Joel);
        story.ParentId.ShouldNotBeNull();
        story.SprintId.ShouldNotBeNull();
        story.AssigneeId.ShouldNotBeNull().ShouldNotBe(SeedIds.Users.JiraImport);
    }

    [Test]
    [Order(3)]
    public async Task Mapping_WhenUserUnknown_ShouldCreateWithoutPassword()
    {
        var story = await ApplyStoryChainAsync();

        var nina = await Host.Services.GetRequiredService<IUserDirectory>().FindByEmailAsync("nina@acme.io");
        nina.ShouldNotBeNull().Status.ShouldBe(UserStatus.NoPassword);
        nina.Name.ShouldBe("Nina New");
        story.AssigneeId.ShouldBe(nina.Id);
    }

    [Test]
    [Order(4)]
    public async Task Apply_WhenChangelogHasStatusAndSummary_ShouldWriteImportedRevisionsByTheirAuthors()
    {
        var story = await ApplyStoryChainAsync();

        var revisions = await Host.Services.CreateAsyncScope().ServiceProvider.GetRequiredService<IWorkItemStore>().GetRevisionsAsync(story.Id);
        var imported = revisions.Where(r => r.ImportJobId == Job && r.Changes.Any(c => c.Field is FieldIds.State or FieldIds.Title)).ToList();
        imported.Select(r => r.ChangedBy).ShouldContain(SeedIds.Users.Joel);
        imported.SelectMany(r => r.Changes).Single(c => c.Field == FieldIds.State).ToLabel.ShouldBe("In Review");
        imported.SelectMany(r => r.Changes).Single(c => c.Field == FieldIds.Title).To.GetString().ShouldBe("Summary of PLAT-11");
        revisions.ShouldAllBe(r => r.OriginalSystem == "jira" || r.Rev == 1 || r.OriginalSystem == null);
    }

    [Test]
    [Order(5)]
    public async Task Apply_WhenAppliedTwice_ShouldUpdateNotDuplicate()
    {
        var first = await ApplyStoryChainAsync();

        var second = await ApplyStoryChainAsync();

        second.Id.ShouldBe(first.Id);
        await using var connection = await OpenDatabaseAsync();
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM work_item WHERE original_system_id = 'PLAT-11'")).ShouldBe(1);
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM sprint WHERE project_id = @p AND name = 'Sprint 1' AND archived_at IS NULL", new { p = SeedIds.Projects.Empt })).ShouldBe(1);
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM app_user WHERE email = 'nina@acme.io'")).ShouldBe(1);
    }

    [Test]
    [Order(6)]
    public async Task Apply_WhenSprintUndated_ShouldScheduleAfterTheLastDatedSprint()
    {
        await ApplyStoryChainAsync();

        await using var connection = await OpenDatabaseAsync();
        var dates = await connection.QuerySingleAsync<(DateOnly Start, DateOnly End)>(
            "SELECT start_date AS Start, end_date AS End FROM sprint WHERE project_id = @p AND name = 'Sprint 2' AND archived_at IS NULL", new { p = SeedIds.Projects.Empt });
        dates.Start.ShouldBe(DateOnly.FromDateTime(Sprint1.EndDate!.Value.UtcDateTime).AddDays(1));
        (dates.End.DayNumber - dates.Start.DayNumber).ShouldBe(13);
    }

    [Test]
    [Order(7)]
    public async Task Apply_WhenCommentsApplied_ShouldAttributeToMatchedCreatedOrImportUser()
    {
        var itemId = (await ApplyStoryChainAsync()).Id;

        var written = await WithApplierAsync((applier, run, _) => applier.ApplyCommentsAsync(run, Story, itemId));

        written.ShouldBe(3);
        var imported = CommentStore.Imported.Where(c => c.WorkItemId == itemId).Select(c => c.Comment).DistinctBy(c => c.OriginalSystemId).ToDictionary(c => c.OriginalSystemId);
        imported["c1"].AuthorId.ShouldBe(SeedIds.Users.Joel);
        imported["c2"].AuthorId.ShouldNotBeNull().ShouldNotBe(SeedIds.Users.JiraImport);
        imported["c3"].AuthorId.ShouldBe(SeedIds.Users.JiraImport);
        imported["c1"].OriginalSystem.ShouldBe("jira");
        imported["c1"].CreatedAt.ShouldBe(JiraWorld.Day);
    }

    [Test]
    [Order(7)]
    public async Task Apply_WhenCommentsApplied_ShouldPublishCommentsChangedOncePerItem()
    {
        var itemId = (await ApplyStoryChainAsync()).Id;
        var seen = new List<CommentsChanged>();
        using var subscription = Host.Services.GetRequiredService<IProjectEvents>().Subscribe(SeedIds.Projects.Empt, projectEvent =>
        {
            if (projectEvent is CommentsChanged changed)
            {
                lock (seen)
                {
                    seen.Add(changed);
                }
            }

            return Task.CompletedTask;
        });

        await WithApplierAsync((applier, run, _) => applier.ApplyCommentsAsync(run, Story, itemId));
        // Delivery is off the publishing call: a later event of ours arrives after the comments one, so waiting for it is not a sleep.
        var marker = new TaskCompletionSource();
        using var done = Host.Services.GetRequiredService<IProjectEvents>().Subscribe(SeedIds.Projects.Empt, projectEvent =>
        {
            if (projectEvent is ProjectChanged)
            {
                marker.TrySetResult();
            }

            return Task.CompletedTask;
        });
        await Host.Services.GetRequiredService<IProjectEvents>().PublishAsync(new ProjectChanged(SeedIds.Projects.Empt));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var _ = deadline.Token.Register(() => marker.TrySetCanceled());
        await marker.Task;

        lock (seen)
        {
            seen.ShouldBe([new CommentsChanged(SeedIds.Projects.Empt, itemId)]);
        }
    }

    [Test]
    [Order(8)]
    public async Task Apply_WhenAttachmentsApplied_ShouldPassBytesAndAttributeToImportUser()
    {
        var itemId = (await ApplyStoryChainAsync()).Id;

        var written = await WithApplierAsync((applier, run, _) => applier.ApplyAttachmentsAsync(run, Story, itemId));

        written.ShouldBe(1);
        var (_, attachment, bytes) = AttachmentStore.Imported.Last(a => a.WorkItemId == itemId);
        Encoding.UTF8.GetString(bytes).ShouldBe("hello");
        (attachment.OriginalSystem, attachment.OriginalSystemId, attachment.Name, attachment.UploadedById).ShouldBe(("jira", "att-1", "spec.txt", (Guid?)SeedIds.Users.JiraImport));
    }

    [Test]
    [Order(9)]
    public async Task Apply_WhenCommentsOff_ShouldWriteNone()
    {
        var itemId = (await ApplyStoryChainAsync()).Id;
        var before = CommentStore.Imported.Count;
        var off = _mapping with { Fields = _mapping.Fields with { IncludeComments = false, IncludeAttachments = false } };

        var written = await WithApplierAsync(off, async (applier, run, _) => await applier.ApplyCommentsAsync(run, Story, itemId) + await applier.ApplyAttachmentsAsync(run, Story, itemId));

        written.ShouldBe(0);
        CommentStore.Imported.Count.ShouldBe(before);
    }

    [Test]
    [Order(10)]
    public async Task Apply_WhenParentNotAppliedYet_ShouldNameItThenLinkOnTheSecondPass()
    {
        var outcomes = await WithApplierAsync(async (applier, run, services) =>
        {
            await applier.PrepareAsync(run, [Sprint1, Sprint2]);
            var first = await applier.ApplyIssueAsync(run, EarlyChild);
            await applier.ApplyIssueAsync(run, LateParent);
            var second = await applier.ApplyIssueAsync(run, EarlyChild);
            var item = await services.GetRequiredService<IWorkItemStore>().GetAsync(second.ItemId!.Value);
            return (first, second, item, run.Items["PLAT-20"]);
        });

        outcomes.first.UnresolvedKeys.ShouldBe(["PLAT-20"]);
        outcomes.second.UnresolvedKeys.ShouldBeEmpty();
        outcomes.second.Created.ShouldBeFalse();
        outcomes.item.ParentId.ShouldBe(outcomes.Item4);
        outcomes.item.Type.ShouldBe(WorkItemTypeId.Task);
        outcomes.item.AssigneeId.ShouldBe(SeedIds.Users.JiraImport);
    }

    [Test]
    [Order(11)]
    public async Task Apply_WhenTypeChangedBetweenRuns_ShouldWriteImportErrorNotThrow()
    {
        await ApplyStoryChainAsync();
        var asBug = _mapping with { IssueTypes = [.. _mapping.IssueTypes.Select(t => t.JiraType == "Story" ? t with { Target = ImportTypeTarget.Bug } : t)] };
        var changed = await WithApplierAsync(asBug, async (applier, run, _) =>
        {
            await applier.PrepareAsync(run, [Sprint1, Sprint2]);
            return await applier.ApplyIssueAsync(run, Story);
        });

        changed.Failed.ShouldBeTrue();
        changed.ItemId.ShouldBeNull();
        await using var connection = await OpenDatabaseAsync();
        var error = await connection.QuerySingleAsync<(string Step, string Entity, string Message, string Resolution)>(
            "SELECT step AS Step, entity AS Entity, message AS Message, resolution AS Resolution FROM import_error WHERE import_job_id = @Job AND source_key = 'PLAT-11'", new { Job });
        (error.Step, error.Entity, error.Resolution).ShouldBe(("issues", "issue", "pending"));
        error.Message.ShouldContain("type");
    }

    [Test]
    [Order(12)]
    public async Task Apply_WhenTypeMapsToSkip_ShouldNotImportIt()
    {
        var skip = _mapping with { IssueTypes = [.. _mapping.IssueTypes.Select(t => t.JiraType == "Sub-task" ? t with { Target = ImportTypeTarget.Skip } : t)] };

        var outcome = await WithApplierAsync(skip, (applier, run, _) => applier.ApplyIssueAsync(run, Sub));

        outcome.ShouldBe(ApplyOutcome.Skipped);
    }

    [Test]
    [Order(13)]
    public void JiraImportUser_ShouldBeTheSeededAttributionUser()
    {
        JiraImportUser.Id.ShouldBe(SeedIds.Users.JiraImport);
    }
}
