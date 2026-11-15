using System.Text.Json;
using ShipLean.Contracts.Inspection;
using ShipLean.WebApi.Tests.Taskboard;

namespace ShipLean.WebApi.Tests.Inspection.Views;

// D4: with show_content off no K5 route and no MCP tool sends the run's prompts, tool input or tool output, whether as a field or inside an
// attribute or a joined log. Looks at every string a response holds, so a new field cannot leak by being forgotten here.
[TestFixture]
public class ContentHiddenTests : InspectionViewsFixture
{
    private string[] _rowIds = [];

    private async Task<string[]> RowIdsAsync()
    {
        var page = await Api.GetAsync<RunPageDto>($"{RunsUrl}/{RunId}");
        return [.. page.Turns.SelectMany(t => t.Rows.Select(r => r.Id))];
    }

    private async Task<List<string>> RestStringsAsync()
    {
        var strings = new List<string>();
        foreach (var url in new[] { RunsUrl, $"{RunsUrl}/{RunId}", $"{RunsUrl}/{RunId}?errorsOnly=true" }.Concat(_rowIds.Select(id => $"{RunsUrl}/{RunId}/rows/{id}")))
        {
            using var response = await Api.GetAsync(url);
            response.EnsureSuccessStatusCode();
            strings.AddRange(Strings(JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement));
        }
        return strings;
    }

    private async Task<List<string>> McpStringsAsync()
    {
        var token = await ToolTokenAsync();
        var strings = new List<string>();
        foreach (var (tool, arguments) in new[]
        {
            ("list_runs", $$"""{"item":"{{Item}}"}"""),
            ("get_run", $$"""{"item":"{{Item}}","run":"{{RunId}}"}"""),
            ("get_run", $$"""{"item":"{{Item}}","run":"{{RunId}}","filter":"errors"}"""),
        })
        {
            var reply = await CallToolAsync(token, tool, arguments);
            reply.IsToolError().ShouldBeFalse(reply.Body);
            strings.AddRange(Strings(reply.Structured()));
            strings.AddRange(Strings(reply.TextJson()));
        }
        return strings;
    }

    [Test]
    public async Task Routes_WhenContentIsOff_ShouldNotCarryAnyContent()
    {
        using var saved = await SetContentAsync(false);
        saved.EnsureSuccessStatusCode();
        _rowIds = await RowIdsAsync();

        var strings = await RestStringsAsync();

        _rowIds.Length.ShouldBeGreaterThan(5);
        foreach (var content in ContentTexts)
        {
            strings.Where(s => s.Contains(content, StringComparison.Ordinal)).ShouldBeEmpty(content);
        }
    }

    [Test]
    public async Task McpTools_WhenContentIsOff_ShouldNotCarryAnyContent()
    {
        using var saved = await SetContentAsync(false);
        saved.EnsureSuccessStatusCode();

        var strings = await McpStringsAsync();

        strings.ShouldContain("Edit");
        foreach (var content in ContentTexts)
        {
            strings.Where(s => s.Contains(content, StringComparison.Ordinal)).ShouldBeEmpty(content);
        }
    }

    [Test]
    public async Task Routes_WhenContentIsOn_ShouldCarryTheContent()
    {
        using var saved = await SetContentAsync(true);
        saved.EnsureSuccessStatusCode();
        _rowIds = await RowIdsAsync();

        var strings = await RestStringsAsync();

        strings.ShouldContain(s => s.Contains("Implement the thing in SHIP-1", StringComparison.Ordinal));
        strings.ShouldContain(s => s.Contains("edited Foo.cs", StringComparison.Ordinal));
        strings.ShouldContain(s => s.Contains("error CS1002", StringComparison.Ordinal));
    }

    [Test]
    public async Task RowDetail_WhenContentIsOff_ShouldSayContentIsHidden()
    {
        using var saved = await SetContentAsync(false);
        saved.EnsureSuccessStatusCode();
        var edit = (await Api.GetAsync<RunPageDto>($"{RunsUrl}/{RunId}")).Turns[0].Rows.First(r => r.Name == "Edit");

        var detail = await Api.GetAsync<RunRowDetailDto>($"{RunsUrl}/{RunId}/rows/{edit.Id}");

        detail.ContentHidden.ShouldBeTrue();
        detail.Input.ShouldBeNull();
        detail.Output.ShouldBeNull();
        detail.FilePath.ShouldBe("src/App/Foo.cs");
        detail.Attributes.Select(a => a.Key).ShouldNotContain("tool_parameters");
        detail.Attributes.Select(a => a.Key).ShouldNotContain("output");
        detail.Logs.SelectMany(l => l.Attributes).Select(a => a.Key).ShouldNotContain("output");
    }
}
