using System.Data.Common;
using ShipLean.Application.Fakes.Holidays;
using ShipLean.Application.Fakes.Projects;
using ShipLean.Application.Fakes.Sprints;
using ShipLean.Contracts;
using ShipLean.Contracts.Sprints;
using ShipLean.TestSupport.Contracts.Sprints;
using ShipLean.UnitTest.Contracts.Projects;

namespace ShipLean.UnitTest.Contracts.Sprints;

public sealed class InMemorySprintStoreContractTests : SprintStoreContract
{
    private InMemoryPlanning _world = null!;
    private InMemoryPublicHolidays _holidays = null!;

    [SetUp]
    public void CreateWorld()
    {
        _world = new InMemoryPlanning();
        _holidays = new InMemoryPublicHolidays();
    }

    protected override ISprintStore CreateStore() => new InMemorySprintStore(_world, _holidays);

    protected override ISprintCapacityRecompute CreateRecompute() => new InMemorySprintCapacityRecompute(_world, _holidays);

    protected override Task ArrangeHolidaysAsync(Guid projectId, string country, params (DateOnly Date, string Name)[] holidays)
    {
        _holidays.AddCountry(country, country);
        foreach (var (date, name) in holidays)
        {
            _holidays.AddHoliday(country, date, name);
        }

        _world.SetHolidayCountry(projectId, country);
        return Task.CompletedTask;
    }

    protected override Task ArrangeWorkingDaysAsync(Guid projectId, params int[] isoDays)
    {
        _world.SetWorkingDays(projectId, isoDays);
        return Task.CompletedTask;
    }

    protected override Task ArrangeStoredCapacityAsync(Guid sprintId, decimal? velocity, params (Guid UserId, decimal? Days, decimal Points)[] rows)
    {
        _world.SetStoredCapacity(sprintId, velocity, rows);
        return Task.CompletedTask;
    }

    protected override Task<Guid> ArrangeProjectAsync() => Task.FromResult(_world.AddProject(new string([.. Guid.NewGuid().ToString("N").Where(char.IsLetter).Take(4).Select(char.ToUpperInvariant)]), "Project"));

    protected override Task<Guid> ArrangeMemberAsync(Guid projectId)
    {
        var user = _world.AddUser("Member " + Guid.NewGuid().ToString("N")[..6], $"{Guid.NewGuid():N}@example.test");
        _world.AddMember(projectId, user, ProjectRole.Member);
        return Task.FromResult(user);
    }

    protected override Task<DbTransaction> BeginTransactionAsync() => Task.FromResult<DbTransaction>(new NoOpTransaction());
}
