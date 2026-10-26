using ShipLean.Application.Fakes.Import;
using ShipLean.Application.Fakes.Users;
using ShipLean.Contracts.Import;
using ShipLean.Contracts.Persisted;
using ShipLean.TestSupport.Contracts.Import;
using ShipLean.TestSupport.Contracts.Users;
using ShipLean.UnitTest.Contracts.Users;

namespace ShipLean.UnitTest.Contracts.Import;

public sealed class InMemoryImportJobsContractTests : ImportJobsContract
{
    private InMemoryImportJobs _jobs = null!;

    [SetUp]
    public void CreateJobs()
    {
        var users = new InMemoryUsers();
        var user = InMemoryUserArranger.Arrange(users, new UserSpec());
        _jobs = new InMemoryImportJobs(TimeProvider.System, new SettableCurrentUser { Id = user.Id }, new InMemoryUserDirectory(users));
    }

    protected override IImportJobs Jobs => _jobs;

    protected override Task<Guid> ArrangeConnectionAsync()
    {
        var id = Guid.NewGuid();
        _jobs.RegisterConnection(id);
        return Task.FromResult(id);
    }

    protected override Task<Guid> ArrangeEmptyProjectAsync()
    {
        var id = Guid.NewGuid();
        _jobs.RegisterEmptyProject(id);
        return Task.FromResult(id);
    }

    protected override Task<Guid> ArrangeNonEmptyProjectAsync()
    {
        var id = Guid.NewGuid();
        _jobs.RegisterNonEmptyProject(id);
        return Task.FromResult(id);
    }

    protected override Task<string> ArrangeTakenProjectKeyAsync()
    {
        _jobs.RegisterProjectKey("TAKN");
        return Task.FromResult("TAKN");
    }

    protected override Task<Guid> ArrangeJobAsync(Guid connectionId, ImportJobState state, int pendingErrors)
        => Task.FromResult(_jobs.ArrangeJob(connectionId, state, pendingErrors));
}
