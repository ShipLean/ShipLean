using Dapper;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Links;

// report_link_state and the title and note of `link`, in SHIP as Joel with a read-write token.
[TestFixture]
public class LinksToolsTests : McpFixture
{
    private const string Address = "https://gitlab.example.test/acme/private/-/merge_requests/9";
    private const string AsOf = "2026-10-25T09:00:00Z";

    private Guid _first;
    private Guid _second;
    private Guid _foreign;
    private string _firstKey = string.Empty;

    protected override bool WithProbeTools => false;

    [OneTimeSetUp]
    public async Task ArrangeItems()
    {
        await using var connection = await OpenDatabaseAsync();
        _first = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithTitle("Has the link").InsertAsync(connection);
        _second = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithTitle("Also has the link").InsertAsync(connection);
        _foreign = await new WorkItemBuilder(SeedIds.Projects.Part).WithType("story").WithTitle("Not Joel's").InsertAsync(connection);
        _firstKey = await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @_first", new { _first });
    }

    [Test]
    public async Task ReportLinkState_ShouldStampEveryMatchingLinkUserCanSee()
    {
        var one = await InsertLinkAsync(_first, Address + "?a");
        var two = await InsertLinkAsync(_second, Address + "?a");
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "report_link_state", $$"""{"url":"{{Address}}?a","title":"Fix the thing","state":"merged","as_of":"{{AsOf}}"}""");

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        foreach (var id in new[] { one, two })
        {
            var row = await ReadLinkAsync(id);
            row.State.ShouldBe("merged");
            row.StateSource.ShouldBe("reported");
            row.StateReportedBy.ShouldBe("Test Agent (unverified)");
            row.StateAsOf.ShouldBe(new DateTimeOffset(2026, 10, 25, 9, 0, 0, TimeSpan.Zero));
            row.PreviewTitle.ShouldBe("Fix the thing");
        }
    }

    [Test]
    public async Task ReportLinkState_WhenOtherProject_ShouldNotTouchIt()
    {
        var mine = await InsertLinkAsync(_first, Address + "?b");
        var theirs = await InsertLinkAsync(_foreign, Address + "?b");
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "report_link_state", $$"""{"url":"{{Address}}?b","state":"closed","as_of":"{{AsOf}}"}""");

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        (await ReadLinkAsync(mine)).State.ShouldBe("closed");
        var other = await ReadLinkAsync(theirs);
        other.State.ShouldBeNull();
        other.StateSource.ShouldBeNull();
    }

    [Test]
    public async Task ReportLinkState_WhenNoLinkMatches_ShouldReturnNotFound()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "report_link_state", $$"""{"url":"https://nobody.example.test/none","state":"open","as_of":"{{AsOf}}"}""");

        reply.IsToolError().ShouldBeTrue();
        reply.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task ReportLinkState_WhenStateIsUnknown_ShouldReturnValidationOnState()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "report_link_state", $$"""{"url":"{{Address}}","state":"stuck","as_of":"{{AsOf}}"}""");

        reply.IsToolError().ShouldBeTrue();
        var error = reply.Structured().GetProperty("error");
        error.GetProperty("code").GetString().ShouldBe(ApiErrorCodes.Validation);
        error.GetProperty("field").GetString().ShouldBe("state");
    }

    [Test]
    public async Task ReportLinkState_WhenAsOfIsNotATime_ShouldReturnValidationOnAsOf()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "report_link_state", $$"""{"url":"{{Address}}","state":"open","as_of":"yesterday"}""");

        reply.IsToolError().ShouldBeTrue();
        reply.Structured().GetProperty("error").GetProperty("field").GetString().ShouldBe("as_of");
    }

    [Test]
    public async Task Link_WhenTitleAndNote_ShouldStoreBoth()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "link", $$"""{"item":"{{_firstKey}}","url":"https://docs.example.test/design","title":"Design doc","note":"Read before reviewing"}""");

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        await using var connection = await OpenDatabaseAsync();
        var row = await connection.QuerySingleAsync<LinkTextRow>(
            "SELECT title, note FROM work_item_link WHERE source_id = @_first AND url = 'https://docs.example.test/design'", new { _first });
        row.ShouldBe(new LinkTextRow("Design doc", "Read before reviewing"));
    }

    [Test]
    public async Task Link_WhenWorkItemTarget_ShouldStillWork()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);
        await using var connection = await OpenDatabaseAsync();
        var secondKey = await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @_second", new { _second });

        var reply = await CallToolAsync(token, "link", $$"""{"item":"{{_firstKey}}","to":"{{secondKey}}","type":"duplicate"}""");

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        reply.Structured().GetProperty("link_type").GetString().ShouldBe("duplicate");
        (await connection.ExecuteScalarAsync<int>(
            "SELECT count(*)::int FROM work_item_link WHERE source_id = @_first AND target_id = @_second AND link_type = 'duplicate'", new { _first, _second })).ShouldBe(1);
    }

    [Test]
    public async Task Link_WhenUrlIsRepeated_ShouldReturnLinkDuplicate()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);
        await InsertLinkAsync(_second, "https://docs.example.test/again");
        var secondKey = await KeyOfAsync(_second);

        var reply = await CallToolAsync(token, "link", $$"""{"item":"{{secondKey}}","url":"https://docs.example.test/again"}""");

        reply.IsToolError().ShouldBeTrue();
        reply.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe(ApiErrorCodes.LinkDuplicate);
    }

    private async Task<string> KeyOfAsync(Guid id)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @id", new { id });
    }

    private async Task<Guid> InsertLinkAsync(Guid workItemId, string url)
    {
        await using var connection = await OpenDatabaseAsync();
        var id = Guid.NewGuid();
        await connection.ExecuteAsync(
            "INSERT INTO work_item_link (id, source_id, link_type, url, created_at) VALUES (@id, @workItemId, 'url', @url, now())", new { id, workItemId, url });
        return id;
    }

    private async Task<ReportedRow> ReadLinkAsync(Guid id)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.QuerySingleAsync<ReportedRow>(
            "SELECT state, state_source AS stateSource, state_as_of AS stateAsOf, state_reported_by AS stateReportedBy, preview_title AS previewTitle FROM work_item_link WHERE id = @id", new { id });
    }

    private sealed record ReportedRow(string? State, string? StateSource, DateTimeOffset? StateAsOf, string? StateReportedBy, string? PreviewTitle);

    private sealed record LinkTextRow(string? Title, string? Note);
}
