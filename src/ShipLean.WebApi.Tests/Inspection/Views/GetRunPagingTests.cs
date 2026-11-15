using System.Text.Json;
using ShipLean.Application.Features.Inspection.Views;

namespace ShipLean.WebApi.Tests.Inspection.Views;

// get_run pages through one flat order, the failed rows first, and says where the next page starts.
[TestFixture]
public class GetRunPagingTests : InspectionViewsFixture
{
    private async Task<JsonElement> PageAsync(string? cursor, int limit, string? filter = null)
    {
        var token = await ToolTokenAsync();
        var cursorArgument = cursor is null ? "" : $$""","cursor":"{{cursor}}" """;
        var filterArgument = filter is null ? "" : $$""","filter":"{{filter}}" """;
        var reply = await CallToolAsync(token, "get_run", $$"""{"item":"{{Item}}","run":"{{RunId}}","limit":{{limit}}{{cursorArgument}}{{filterArgument}}}""");
        reply.IsToolError().ShouldBeFalse(reply.Body);
        return reply.Structured().TryGetProperty("data", out var data) ? data : throw new InvalidOperationException(reply.Structured().GetRawText()[..Math.Min(500, reply.Structured().GetRawText().Length)]);
    }

    private static string[] Ids(JsonElement page) => [.. page.GetProperty("rows").EnumerateArray().Select(r => r.GetProperty("row").GetProperty("id").GetString()!)];

    private static string? Next(JsonElement page) => page.TryGetProperty("nextCursor", out var next) && next.ValueKind == JsonValueKind.String ? next.GetString() : null;

    [Test]
    public async Task GetRun_WhenPagedThroughByCursor_ShouldReturnEveryRowOnceWithErrorsFirst()
    {
        var all = await PageAsync(null, 200);
        var ids = new List<string>();
        string? cursor = null;
        var pages = 0;
        do
        {
            var page = await PageAsync(cursor, 4);
            Ids(page).Length.ShouldBeLessThanOrEqualTo(4);
            ids.AddRange(Ids(page));
            cursor = Next(page);
            pages++;
        }
        while (cursor is not null);

        pages.ShouldBe(3);
        ids.ShouldBe(Ids(all));
        ids.Distinct().Count().ShouldBe(10);
        all.GetProperty("totalRows").GetInt32().ShouldBe(10);
        all.GetProperty("errorRows").GetInt32().ShouldBe(2);
        all.GetProperty("rows").EnumerateArray().Take(2).Select(r => r.GetProperty("row").GetProperty("isError").GetBoolean()).ShouldBe([true, true]);
        all.GetProperty("rows").EnumerateArray().Skip(2).ShouldAllBe(r => !r.GetProperty("row").GetProperty("isError").GetBoolean());
        Next(all).ShouldBeNull();
    }

    [Test]
    public async Task GetRun_WhenFilteredToErrors_ShouldPageOnlyTheFailedRows()
    {
        var page = await PageAsync(null, 1, "errors");

        Ids(page).Length.ShouldBe(1);
        page.GetProperty("totalRows").GetInt32().ShouldBe(2);
        Next(page).ShouldNotBeNull();
        Ids(await PageAsync(Next(page), 1, "errors")).Length.ShouldBe(1);
    }

    [Test]
    public async Task GetRun_WhenTheCursorIsNotOneItReturned_ShouldBeAValidationError()
    {
        var token = await ToolTokenAsync();

        var reply = await CallToolAsync(token, "get_run", $$"""{"item":"{{Item}}","run":"{{RunId}}","cursor":"nope"}""");

        reply.IsToolError().ShouldBeTrue();
        reply.Body.ShouldContain("validation");
    }

    [Test]
    public void Cursor_WhenEncoded_ShouldDecodeToTheSameOffset()
    {
        RunRowsCursor.Decode(RunRowsCursor.Encode(37)).ShouldBe(37);
        RunRowsCursor.Decode(null).ShouldBe(0);
    }

    [Test]
    public async Task GetRun_Description_ShouldTellAgentsToCiteRunIds()
    {
        var token = await ToolTokenAsync();

        var reply = await PostAsync(token, """{"jsonrpc":"2.0","id":1,"method":"tools/list"}""");

        reply.Body.ShouldContain("log_retrospective");
        reply.Body.ShouldContain("errors first");
    }
}
