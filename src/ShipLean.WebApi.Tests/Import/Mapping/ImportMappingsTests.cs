using Npgsql;
using ShipLean.Application.Database;
using ShipLean.Application.Fakes.Import;
using ShipLean.Application.Features.Import.Mapping;
using ShipLean.Application.Features.States;
using ShipLean.Application.Features.Users;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Import;
using ShipLean.Contracts.Import.Jira;
using ShipLean.Contracts.Refs;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Contracts.Users;
using ShipLean.TestSupport.Database;

namespace ShipLean.WebApi.Tests.Import.Mapping;

// The suggested mapping over an in-memory Jira, the real state catalog and user directory (seeded clone), and the saved-job fake.
public sealed class ImportMappingsTests
{
    private static readonly Guid Connection = Guid.NewGuid();

    private ClonedDatabase _database = null!;
    private NpgsqlDataSource _dataSource = null!;
    private InMemoryImportJobs _jobs = null!;
    private InMemoryJiraConnections _connections = null!;

    [OneTimeSetUp]
    public async Task CloneDatabase()
    {
        DapperTypeHandlers.Register();
        _database = await ShipLeanDatabase.CloneSeedAsync();
        _dataSource = NpgsqlDataSource.Create(DatabaseServices.PinUtc(_database.ConnectionString));
    }

    [OneTimeTearDown]
    public async Task DropDatabase()
    {
        await _dataSource.DisposeAsync();
        await _database.DisposeAsync();
    }

    [SetUp]
    public void Arrange()
    {
        _connections = new InMemoryJiraConnections();
        _connections.Arrange(Connection, JiraWorld.Credentials);
        _jobs = new InMemoryImportJobs(TimeProvider.System, new SettableCurrentUser { Id = SeedIds.Users.Joel }, new UserDirectory(_dataSource, TimeProvider.System));
        _jobs.RegisterConnection(Connection);
    }

    private ImportMappings Mappings(InMemoryJiraData data)
        => new(_connections, new InMemoryJiraClient(JiraWorld.Credentials, data), _jobs, new StateCatalog(_dataSource), new UserDirectory(_dataSource, TimeProvider.System));

    [Test]
    public async Task Mapping_WhenEpic_ShouldBecomeFeature()
    {
        var mapping = await Mappings(JiraWorld.Data([JiraWorld.Issue("PLAT-1", "Epic"), JiraWorld.Issue("PLAT-2"), JiraWorld.Issue("PLAT-3", "Bug")]))
            .SuggestAsync(Connection, JiraWorld.ProjectKey, null);

        mapping.IssueTypes.Single(t => t.JiraType == "Epic").Target.ShouldBe(ImportTypeTarget.Feature);
        mapping.IssueTypes.Single(t => t.JiraType == "Epic").Count.ShouldBe(1);
        mapping.IssueTypes.Single(t => t.JiraType == "Story").Target.ShouldBe(ImportTypeTarget.Story);
        mapping.IssueTypes.Single(t => t.JiraType == "Bug").Target.ShouldBe(ImportTypeTarget.Bug);
        mapping.IssueTypes.Single(t => t.JiraType == "Sub-task").Target.ShouldBe(ImportTypeTarget.Task);
        mapping.Totals.Issues.ShouldBe(3);
    }

    [Test]
    public async Task Mapping_WhenStatusUnmapped_ShouldCreateStateWithJiraCategory()
    {
        var statuses = new[]
        {
            JiraWorld.Status("To Do", JiraStatusCategory.New, "Story"),
            JiraWorld.Status("In Progress", JiraStatusCategory.Indeterminate, "Story"),
            JiraWorld.Status("In Review", JiraStatusCategory.Indeterminate, "Story"),
        };

        var mapping = await Mappings(JiraWorld.Data([JiraWorld.Issue("PLAT-1", status: "In Review")], statuses)).SuggestAsync(Connection, JiraWorld.ProjectKey, null);

        var review = mapping.Statuses.Single(s => s.JiraType == "Story" && s.JiraStatus == "In Review");
        review.CreatesState.ShouldBeTrue();
        review.TargetState.ShouldBe("In Review");
        review.TargetCategory.ShouldBe(StateCategory.InProgress);
        review.Count.ShouldBe(1);
        mapping.Warnings.ShouldContain(w => w.Severity == ImportWarningSeverity.Warning && w.Section == ImportMappingSection.Statuses && w.Message.Contains("In Review") && w.Message.Contains("new"));
    }

    [Test]
    public async Task Mapping_WhenStatusSharesCategoryWithState_ShouldLandInThatState()
    {
        var mapping = await Mappings(JiraWorld.Data([JiraWorld.Issue("PLAT-1")])).SuggestAsync(Connection, JiraWorld.ProjectKey, null);

        var todo = mapping.Statuses.Single(s => s.JiraType == "Story" && s.JiraStatus == "To Do");
        (todo.TargetState, todo.TargetCategory, todo.CreatesState).ShouldBe(("New", StateCategory.Proposed, false));
        var done = mapping.Statuses.Single(s => s.JiraType == "Story" && s.JiraStatus == "Done");
        (done.TargetState, done.TargetCategory, done.CreatesState).ShouldBe(("Closed", StateCategory.Completed, false));
        mapping.Statuses.Where(s => s.JiraType == "Story").ShouldAllBe(s => !s.CreatesState);
    }

    [Test]
    public async Task Mapping_WhenUsersDiffer_ShouldMatchCreateOrAttribute()
    {
        var joel = JiraWorld.Person("a-joel", "Joel", "JOEL@shiplean.local ");
        var stranger = JiraWorld.Person("a-new", "Nina New", "nina@acme.io");
        var hidden = JiraWorld.Person("a-hid", "Hidden", null);
        var bot = JiraWorld.Person("a-bot", "Automation", "bot@acme.io", bot: true);
        var issues = new[] { JiraWorld.Issue("PLAT-1", assignee: joel, reporter: stranger), JiraWorld.Issue("PLAT-2", assignee: hidden, reporter: bot), JiraWorld.Issue("PLAT-3", assignee: joel) };

        var mapping = await Mappings(JiraWorld.Data(issues)).SuggestAsync(Connection, JiraWorld.ProjectKey, null);

        var byAccount = mapping.Users.ToDictionary(u => u.JiraAccountId);
        byAccount["a-joel"].Resolution.ShouldBe(UserResolution.Matched);
        byAccount["a-joel"].User.ShouldNotBeNull().Id.ShouldBe(SeedIds.Users.Joel);
        byAccount["a-joel"].References.ShouldBe(2);
        byAccount["a-new"].Resolution.ShouldBe(UserResolution.Create);
        byAccount["a-hid"].Resolution.ShouldBe(UserResolution.JiraImportUser);
        byAccount["a-bot"].Resolution.ShouldBe(UserResolution.JiraImportUser);
        byAccount["a-bot"].IsBot.ShouldBeTrue();
        mapping.Totals.People.ShouldBe(4);
        mapping.Warnings.ShouldContain(w => w.Section == ImportMappingSection.Users && w.Message.Contains("without a password"));
        mapping.Warnings.ShouldContain(w => w.Severity == ImportWarningSeverity.Warning && w.Section == ImportMappingSection.Users && w.Message.Contains("Hidden"));
    }

    [Test]
    public async Task Mapping_WhenTypeHasNoEquivalent_ShouldWarnAndBecomeStory()
    {
        var data = JiraWorld.Data([JiraWorld.Issue("PLAT-1")]) with
        {
            IssueTypes = new Dictionary<string, IReadOnlyList<JiraIssueType>> { [JiraWorld.ProjectKey] = [new JiraIssueType("1", "Spike", false)] },
        };

        var mapping = await Mappings(data).SuggestAsync(Connection, JiraWorld.ProjectKey, null);

        mapping.IssueTypes.Single().Target.ShouldBe(ImportTypeTarget.Story);
        mapping.Warnings.ShouldContain(w => w.Section == ImportMappingSection.Types && w.Severity == ImportWarningSeverity.Warning && w.Message.Contains("Spike"));
    }

    [Test]
    public async Task Mapping_WhenPointsFields_ShouldChooseStoryPointsAndCountPopulated()
    {
        var mapping = await Mappings(JiraWorld.Data([JiraWorld.Issue("PLAT-1", points: 5), JiraWorld.Issue("PLAT-2")])).SuggestAsync(Connection, JiraWorld.ProjectKey, null);

        mapping.Fields.StoryPointsFieldId.ShouldBe(JiraWorld.PointsField);
        mapping.Fields.Candidates.Single(c => c.FieldId == JiraWorld.PointsField).ShouldBe(new StoryPointsFieldDto(JiraWorld.PointsField, "Story Points", 1, 1));
        mapping.Fields.Candidates.Select(c => c.FieldId).ShouldBe([JiraWorld.PointsField, JiraWorld.OtherPointsField]);
    }

    [Test]
    public async Task Mapping_WhenCommentsAndAttachmentsPresent_ShouldCountThemInTotals()
    {
        var first = await Mappings(JiraWorld.Data(
            [JiraWorld.Issue("PLAT-1", attachments: [new JiraAttachment("a1", "a.txt", "text/plain", 3, "https://acme.example.test/a1")])],
            comments: new Dictionary<string, IReadOnlyList<JiraComment>> { ["PLAT-1"] = [new JiraComment("c1", null, "hi", JiraWorld.Day, JiraWorld.Day)] }))
            .SuggestAsync(Connection, JiraWorld.ProjectKey, null);

        first.Totals.Comments.ShouldBe(1);
        first.Totals.Attachments.ShouldBe(1);
    }

    [Test]
    public async Task Mapping_WhenRerun_ShouldFlagOnlyTypesTheLastRunDidNotCover()
    {
        var data = JiraWorld.Data([JiraWorld.Issue("PLAT-1")]);
        var first = await Mappings(data).SuggestAsync(Connection, JiraWorld.ProjectKey, null);
        var chosen = first with { IssueTypes = [.. first.IssueTypes.Where(t => t.JiraType != "Bug").Select(t => t.JiraType == "Epic" ? t with { Target = ImportTypeTarget.Epic } : t)], Statuses = [.. first.Statuses.Where(s => s.JiraType != "Bug")] };
        var job = await _jobs.StartAsync(new ImportStartRequest(Connection, JiraWorld.ProjectKey, new ImportTarget(ImportTargetKind.NewProject, "PLAT", "Platform", null), chosen));
        await _jobs.StopAsync(job.Id);

        var again = await Mappings(data).SuggestAsync(Connection, JiraWorld.ProjectKey, job.Id);

        again.IssueTypes.Where(t => t.IsNewSinceLastRun).Select(t => t.JiraType).ShouldBe(["Bug"]);
        again.IssueTypes.Single(t => t.JiraType == "Epic").Target.ShouldBe(ImportTypeTarget.Epic);
        again.Statuses.ShouldAllBe(s => s.IsNewSinceLastRun == (s.JiraType == "Bug"));
    }

    [Test]
    public async Task Mapping_WhenConnectionUnknown_ShouldBeNotFound()
    {
        var thrown = await Should.ThrowAsync<ApiException>(() => Mappings(JiraWorld.Data([])).SuggestAsync(Guid.NewGuid(), JiraWorld.ProjectKey, null));

        thrown.Code.ShouldBe(ApiErrorCodes.NotFound);
    }
}
