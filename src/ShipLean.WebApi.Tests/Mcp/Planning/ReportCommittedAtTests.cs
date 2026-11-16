using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport.TaskChanges;
using ShipLean.WebApi.Tests.Mcp.Planning.Reports;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Planning;

// report_task's committed_at (SHIP-1398 K1): stored as UTC on the commit's link. A commit of a repository the token can't read stays a link
// (acme/secret on the fake code host), which is where the time lives.
[TestFixture]
public class ReportCommittedAtTests : ReportTestBase
{
    private const string Secret = "github.com/acme/secret";

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        base.ConfigureTestServices!(services);
        services.AddFakeCodeHost();
    };

    [OneTimeSetUp]
    public Task Seed() => SeedPlanAsync();

    // Mutation: keep the given offset instead of converting to UTC.
    [Test]
    public async Task Report_WhenCommittedAtGiven_ShouldStoreUtcOnCommitLink()
    {
        var task = NextTask();

        var reply = await ReportAsync(Body(task, "2026-11-16T09:30:00+02:00"));

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        var stored = await CommittedAtOfAsync(task);
        stored.ShouldBe(new DateTimeOffset(2026, 11, 16, 7, 30, 0, TimeSpan.Zero));
        stored!.Value.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Test]
    public async Task Report_WhenCommittedAtHasNoOffset_ShouldReturnValidationNamingField()
    {
        var task = RefusedTask;

        var reply = await ReportAsync(Body(task, "2026-11-16T09:30:00"));

        ErrorOf(reply).ShouldBe((ApiErrorCodes.Validation, "commits[0].committed_at"));
        (await LinksOfAsync(task)).ShouldBeEmpty();
    }

    // Mutation: drop the committed_at IS NULL guard.
    [Test]
    public async Task Report_WhenReportedAgainWithDifferentTime_ShouldKeepFirstTime()
    {
        var task = NextTask();
        (await ReportAsync(Body(task, "2026-11-16T09:30:00+02:00"))).IsToolError().ShouldBeFalse();

        var again = await ReportAsync(Body(task, "2026-11-15T01:00:00Z"));

        again.IsToolError().ShouldBeFalse(again.ResultText());
        (await CommittedAtOfAsync(task)).ShouldBe(new DateTimeOffset(2026, 11, 16, 7, 30, 0, TimeSpan.Zero));
        (await LinksOfAsync(task)).Count.ShouldBe(1);
    }

    [Test]
    public async Task Report_WhenReportedAgainWithTimeAfterNone_ShouldSetIt()
    {
        var task = NextTask();
        (await ReportAsync(Body(task, null))).IsToolError().ShouldBeFalse();
        (await CommittedAtOfAsync(task)).ShouldBeNull();

        (await ReportAsync(Body(task, "2026-11-16T07:30:00Z"))).IsToolError().ShouldBeFalse();

        (await CommittedAtOfAsync(task)).ShouldBe(new DateTimeOffset(2026, 11, 16, 7, 30, 0, TimeSpan.Zero));
    }

    [Test]
    public async Task Report_WhenCommittedAtMoreThanADayAhead_ShouldReturnValidationNamingField()
    {
        var task = RefusedTask;

        var reply = await ReportAsync(Body(task, "2099-01-01T00:00:00Z"));

        ErrorOf(reply).ShouldBe((ApiErrorCodes.Validation, "commits[0].committed_at"));
    }

    private static string Body(string task, string? committedAt)
    {
        var at = committedAt is null ? string.Empty : ",\"committed_at\":\"" + committedAt + "\"";
        return $$"""{"item":"{{task}}","status":"partial","commits":[{"repo":"{{Secret}}","sha":"{{FakeCodeHost.Shas.First}}","subject":"Hidden"{{at}}}]}""";
    }

    private async Task<DateTimeOffset?> CommittedAtOfAsync(string key)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.QuerySingleAsync<DateTimeOffset?>(
            "SELECT l.committed_at FROM work_item_link l JOIN work_item w ON w.id = l.source_id WHERE w.key = @key AND l.link_type = 'url' AND l.archived_at IS NULL",
            new { key });
    }

    private static (string? Code, string? Field) ErrorOf(McpReply reply)
    {
        reply.IsToolError().ShouldBeTrue();
        var error = reply.Structured().GetProperty("error");
        return (error.GetProperty("code").GetString(), error.GetProperty("field").GetString());
    }
}
