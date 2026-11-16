using Dapper;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Planning.Reports;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.InspectionMetrics;

// backfill_commit_times (SHIP-1398 A2, item D13) over the real tables: commit links made by report_task, one with a time and two without.
[TestFixture]
public class BackfillCommitTimesToolTests : ReportTestBase
{
    private const string WithTime = "a1b2c3d4e5";
    private const string Missing = "b2c3d4e5f6";
    private const string FullShaMissing = "c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1d2";
    private const string Url = "https://git.example.test/acme/ship/commit/";
    private const string SetAt = "2026-10-05T09:30:00+02:00";

    private string _task = string.Empty;

    [OneTimeSetUp]
    public async Task Arrange()
    {
        await SeedPlanAsync();
        _task = NextTask();
        (await PickUpAsync(_task)).IsToolError().ShouldBeFalse();
        var reported = await ReportAsync($$"""
            {"item":"{{_task}}","status":"partial","commits":[
              {"sha":"{{WithTime}}","subject":"Has a time","url":"{{Url}}{{WithTime}}","committed_at":"{{SetAt}}"},
              {"sha":"{{Missing}}","subject":"No time","url":"{{Url}}{{Missing}}"},
              {"sha":"{{FullShaMissing}}","subject":"Full sha","url":"{{Url}}{{FullShaMissing}}"}]}
            """);
        reported.IsToolError().ShouldBeFalse(reported.ResultText());
    }

    [Test]
    public async Task Call_WhenNotInstanceAdmin_ShouldReturnNotFound()
    {
        var token = await TokenAsync(TestUser.Priya, ReadWrite);

        var listing = await CallToolAsync(token, "backfill_commit_times");
        var setting = await CallToolAsync(token, "backfill_commit_times", $$"""{"commits":[{"sha":"{{Missing}}","committed_at":"{{SetAt}}"}]}""");

        listing.IsToolError().ShouldBeTrue();
        listing.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe(ApiErrorCodes.NotFound);
        setting.IsToolError().ShouldBeTrue();
        setting.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe(ApiErrorCodes.NotFound);
        (await TimeOfAsync(Missing)).ShouldBeNull();
    }

    [Test]
    public async Task Call_WhenListing_ShouldReturnOnlyLinksWithoutTime()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "backfill_commit_times", """{"limit":500}""");

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        var missing = reply.Structured().GetProperty("missing").EnumerateArray()
            .Select(link => (Sha: link.GetProperty("sha7").GetString()!, Url: link.GetProperty("url").GetString()!, Item: link.GetProperty("item").GetString()!))
            .ToList();
        missing.ShouldContain((Missing[..7], Url + Missing, _task));
        missing.ShouldContain((FullShaMissing[..7], Url + FullShaMissing, _task));
        missing.Select(link => link.Sha).ShouldNotContain(WithTime[..7]);
    }

    [Test]
    public async Task Call_WhenTimeAlreadySet_ShouldNotOverwrite()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "backfill_commit_times", $$"""{"commits":[{"sha":"{{WithTime}}","committed_at":"2020-01-01T00:00:00Z"},{"sha":"deadbee","committed_at":"2020-01-01T00:00:00Z"}]}""");

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        reply.Structured().GetProperty("updated").GetInt32().ShouldBe(0);
        reply.Structured().GetProperty("already_set").GetInt32().ShouldBe(1);
        reply.Structured().GetProperty("not_found").GetInt32().ShouldBe(1);
        (await TimeOfAsync(WithTime)).ShouldBe(DateTimeOffset.Parse(SetAt).ToUniversalTime());
    }

    [Test]
    public async Task Call_WhenShaIsFull_ShouldMatchSevenCharacterTitle()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "backfill_commit_times", $$"""{"commits":[{"sha":"{{FullShaMissing}}","committed_at":"2026-10-06T10:00:00+02:00"}]}""");

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        reply.Structured().GetProperty("updated").GetInt32().ShouldBe(1);
        (await TimeOfAsync(FullShaMissing)).ShouldBe(new DateTimeOffset(2026, 10, 6, 8, 0, 0, TimeSpan.Zero));
    }

    private async Task<DateTimeOffset?> TimeOfAsync(string sha)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.QuerySingleAsync<DateTimeOffset?>(
            "SELECT l.committed_at FROM work_item_link l JOIN work_item w ON w.id = l.source_id WHERE w.key = @key AND l.title LIKE @title",
            new { key = _task, title = $"commit {sha[..7]} %" });
    }
}
