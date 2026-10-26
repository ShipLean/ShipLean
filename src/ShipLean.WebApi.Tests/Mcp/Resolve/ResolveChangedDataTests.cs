using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Features.Mcp.Resolve;

namespace ShipLean.WebApi.Tests.Mcp.Resolve;

// Resolution after the data changed: archived sprints and states never match, and an exact name beats a prefix.
[TestFixture]
public class ResolveChangedDataTests : ApiFixture
{
    private SprintResolver Sprints => Host.Services.GetRequiredService<SprintResolver>();

    private StateResolver States => Host.Services.GetRequiredService<StateResolver>();

    private MemberResolver Members => Host.Services.GetRequiredService<MemberResolver>();

    private string _twinFirstEmail = string.Empty;

    [OneTimeSetUp]
    public async Task ArrangeArchivedAndExtraRows()
    {
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync("UPDATE sprint SET archived_at = now() WHERE id = @id", new { id = SeedIds.Sprints.Sprint19 });
        await connection.ExecuteAsync("UPDATE state SET archived_at = now() WHERE id = @id", new { id = SeedIds.States.Ship.BugTriaged });
        await connection.ExecuteAsync(
            "INSERT INTO sprint (id, project_id, name, start_date, end_date) VALUES (@id, @project, 'Sprint 2', DATE '2020-01-01', DATE '2020-01-14')",
            new { id = Guid.NewGuid(), project = SeedIds.Projects.Ship });
        await connection.ExecuteAsync(
            "INSERT INTO sprint (id, project_id, name, start_date, end_date) VALUES (@id, @project, 'Sprint 190', DATE '2020-02-01', DATE '2020-02-14')",
            new { id = Guid.NewGuid(), project = SeedIds.Projects.Ship });
        await connection.ExecuteAsync(
            "INSERT INTO state (id, project_id, type, name, category, sort_order) VALUES (@id, @project, 'bug', 'Triaged later', 'inProgress', 9)",
            new { id = Guid.NewGuid(), project = SeedIds.Projects.Ship });

        var twinFirst = new AppUserBuilder().WithName("Twin Name");
        var twinSecond = new AppUserBuilder().WithName("Twin Name");
        var gone = new AppUserBuilder().WithName("Dana Lee").DisabledAt(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var longer = new AppUserBuilder().WithName("Dana Leeson");
        _twinFirstEmail = twinFirst.Email;
        foreach (var user in new[] { twinFirst, twinSecond, gone, longer })
        {
            var id = await user.InsertAsync(connection);
            await connection.ExecuteAsync(
                "INSERT INTO project_member (id, project_id, user_id, role) VALUES (@id, @project, @user, 'member')",
                new { id = Guid.NewGuid(), project = SeedIds.Projects.Ship, user = id });
        }
    }

    [Test]
    public async Task Resolve_WhenSprintNameExact_ShouldWinOverPrefix()
    {
        var sprint = await Sprints.ResolveAsync(SeedIds.Projects.Ship, "sprint 2");

        sprint.Name.ShouldBe("Sprint 2");
    }

    [Test]
    public async Task Resolve_WhenSprintArchived_ShouldNotMatch()
    {
        var error = await Should.ThrowAsync<ApiException>(() => Sprints.ResolveAsync(SeedIds.Projects.Ship, "Sprint 19"));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Resolve_WhenStateArchived_ShouldNotMatch()
    {
        var error = await Should.ThrowAsync<ApiException>(() => States.ResolveAsync(SeedIds.Projects.Ship, WorkItemTypeId.Bug, "Triaged"));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Resolve_WhenArchivedSprintNamedExactly_ShouldBeNotFound()
    {
        var error = await Should.ThrowAsync<ApiException>(() => Sprints.ResolveAsync(SeedIds.Projects.Ship, "sprint 19"));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
        error.Message.ShouldContain("archived");
    }

    [Test]
    public async Task Resolve_WhenArchivedStateNamedExactly_ShouldBeNotFound()
    {
        var error = await Should.ThrowAsync<ApiException>(() => States.ResolveAsync(SeedIds.Projects.Ship, WorkItemTypeId.Bug, "triaged"));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
        error.Message.ShouldContain("archived");
    }

    [Test]
    public async Task Resolve_WhenDisabledMemberNamedExactly_ShouldBeNotFound()
    {
        var error = await Should.ThrowAsync<ApiException>(() => Members.ResolveAsync(SeedIds.Projects.Ship, "Dana Lee"));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
        error.Message.ShouldContain("archived");
    }

    [Test]
    public async Task Resolve_WhenTwoMembersShareAName_ShouldListNameAndEmail()
    {
        var error = await Should.ThrowAsync<ApiException>(() => Members.ResolveAsync(SeedIds.Projects.Ship, "Twin Name"));

        error.Code.ShouldBe(ApiErrorCodes.Ambiguous);
        error.Candidates!.Count.ShouldBe(2);
        error.Candidates.ShouldAllBe(c => c.StartsWith("Twin Name <", StringComparison.Ordinal));
        var first = await Members.ResolveAsync(SeedIds.Projects.Ship, error.Candidates.Single(c => c.Contains(_twinFirstEmail, StringComparison.OrdinalIgnoreCase)));
        first.Email.ShouldBe(_twinFirstEmail);
        first.Exact.ShouldBeTrue();
    }
}
