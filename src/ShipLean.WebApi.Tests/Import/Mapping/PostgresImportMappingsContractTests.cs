using Npgsql;
using ShipLean.Application.Database;
using ShipLean.Application.Fakes.Import;
using ShipLean.Application.Features.Import.Mapping;
using ShipLean.Application.Features.States;
using ShipLean.Application.Features.Users;
using ShipLean.Contracts.Import;
using ShipLean.Contracts.Import.Jira;
using ShipLean.Contracts.Refs;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Contracts.Import;
using ShipLean.TestSupport.Contracts.Users;
using ShipLean.TestSupport.Database;

namespace ShipLean.WebApi.Tests.Import.Mapping;

// The mappings contract on Postgres: the seed clone for the user directory and state catalog, an in-memory Jira, and the saved-job fake for the earlier run.
public sealed class PostgresImportMappingsContractTests : ImportMappingsContract
{
    private const string NewStatus = "Waiting for Vendor";
    private const string ChosenStatus = "Selected for Development";
    private const string ChosenTargetState = "Ready";
    private const string SamePerson = "acct-same";
    private const string NewUser = "acct-new";

    private ClonedDatabase _database = null!;
    private NpgsqlDataSource _dataSource = null!;
    private ImportMappings _mappings = null!;

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

    protected override IImportMappings Mappings => _mappings;

    protected override async Task<MappingWorld> ArrangeAsync()
    {
        var directory = new UserDirectory(_dataSource, TimeProvider.System);
        var statuses = new[]
        {
            JiraWorld.Status("To Do", JiraStatusCategory.New, "Story"),
            JiraWorld.Status(ChosenStatus, JiraStatusCategory.Indeterminate, "Story"),
            JiraWorld.Status(NewStatus, JiraStatusCategory.Indeterminate, "Story"),
        };
        var priya = JiraWorld.Person(SamePerson, "P. Shah", "p.shah@acme.io");
        var issues = new[]
        {
            JiraWorld.Issue("PLAT-1", status: "To Do", assignee: JiraWorld.Person("acct-joel", "Joel", "joel@shiplean.local"), reporter: priya),
            JiraWorld.Issue("PLAT-2", status: ChosenStatus, assignee: JiraWorld.Person(NewUser, "Nina New", "nina@acme.io")),
            JiraWorld.Issue("PLAT-3", status: NewStatus),
        };
        var connectionId = Guid.NewGuid();
        var connections = new InMemoryJiraConnections();
        connections.Arrange(connectionId, JiraWorld.Credentials);
        var jobs = new InMemoryImportJobs(TimeProvider.System, new SettableCurrentUser { Id = SeedIds.Users.Joel }, directory);
        jobs.RegisterConnection(connectionId);
        _mappings = new ImportMappings(connections, new InMemoryJiraClient(JiraWorld.Credentials, JiraWorld.Data(issues, statuses)), jobs, new StateCatalog(_dataSource), directory);

        var fresh = await _mappings.SuggestAsync(connectionId, JiraWorld.ProjectKey, null);
        var priyaRef = new UserRef(SeedIds.Users.Priya, "Priya Shah", "PS", "#5b6b8c", null);
        var then = fresh with
        {
            Statuses = [.. fresh.Statuses.Where(s => s.JiraStatus != NewStatus).Select(s => s.JiraStatus == ChosenStatus ? s with { TargetState = ChosenTargetState } : s)],
            Users = [.. fresh.Users.Where(u => u.JiraAccountId != NewUser).Select(u => u.JiraAccountId == SamePerson ? u with { Resolution = UserResolution.Existing, User = priyaRef } : u)],
            Fields = fresh.Fields with { StoryPointsFieldId = JiraWorld.OtherPointsField },
        };
        var previous = await jobs.StartAsync(new ImportStartRequest(connectionId, JiraWorld.ProjectKey, new ImportTarget(ImportTargetKind.NewProject, "PLAT", "Platform", null), then));
        await jobs.StopAsync(previous.Id);
        return new MappingWorld(connectionId, JiraWorld.ProjectKey, previous.Id, NewStatus, NewUser, JiraWorld.OtherPointsField, ChosenStatus, ChosenTargetState, SamePerson, SeedIds.Users.Priya);
    }
}
