using ShipLean.Application.Fakes.Import;
using ShipLean.Application.Fakes.Users;
using ShipLean.Contracts.Import;
using ShipLean.TestSupport.Contracts.Import;
using ShipLean.TestSupport.Contracts.Users;
using ShipLean.UnitTest.Contracts.Users;
using ShipLean.UnitTest.Dtos.Import;

namespace ShipLean.UnitTest.Contracts.Import;

public sealed class InMemoryImportMappingsContractTests : ImportMappingsContract
{
    private const string NewStatus = "Waiting for Vendor";
    private const string NewUser = "712020:7a1b4c6d-1f0e-4a52-8c3b-9d2e5f6a7b80";
    private const string SamePerson = "712020:2e9c8b7a-6d5f-4e3a-9b1c-0a8f7e6d5c4b";
    private const string ChosenStatus = "Selected for Development";
    private const string ChosenTargetState = "Ready";
    private const string ChosenField = "customfield_10028";

    private InMemoryImportJobs _jobs = null!;
    private InMemoryImportMappings _mappings = null!;

    [SetUp]
    public void CreateMappings()
    {
        var users = new InMemoryUsers();
        var user = InMemoryUserArranger.Arrange(users, new UserSpec());
        _jobs = new InMemoryImportJobs(TimeProvider.System, new SettableCurrentUser { Id = user.Id }, new InMemoryUserDirectory(users));
        _mappings = new InMemoryImportMappings(_jobs);
    }

    protected override IImportMappings Mappings => _mappings;

    // The suggestion is the fixture with the defaults for the choices the earlier run changed; the earlier run mapped everything but one
    // status and one user, used another story points field, sent a status to another state and matched a user to an existing one.
    protected override async Task<MappingWorld> ArrangeAsync()
    {
        var fixture = ImportDtoFixture.Read<ImportMappingDto>("mapping-suggested.json");
        var samePerson = fixture.Users.Single(u => u.JiraAccountId == SamePerson);
        var suggested = fixture with
        {
            Users = [.. fixture.Users.Select(u => u.JiraAccountId == SamePerson ? u with { Resolution = UserResolution.Create, User = null } : u)],
        };
        var then = fixture with
        {
            Statuses = [.. fixture.Statuses.Where(s => s.JiraStatus != NewStatus).Select(s => s.JiraStatus == ChosenStatus ? s with { TargetState = ChosenTargetState } : s)],
            Users = [.. fixture.Users.Where(u => u.JiraAccountId != NewUser)],
            Fields = fixture.Fields with { StoryPointsFieldId = ChosenField },
        };
        var connectionId = Guid.NewGuid();
        _jobs.RegisterConnection(connectionId);
        var target = new ImportTarget(ImportTargetKind.NewProject, "PLAT", "Platform", null);
        var previous = await _jobs.StartAsync(new ImportStartRequest(connectionId, "PLATFORM", target, then));
        await _jobs.StopAsync(previous.Id);
        _mappings.ArrangeSuggestion(connectionId, "PLATFORM", suggested);
        return new MappingWorld(connectionId, "PLATFORM", previous.Id, NewStatus, NewUser, ChosenField, ChosenStatus, ChosenTargetState, SamePerson, samePerson.User!.Id);
    }
}
