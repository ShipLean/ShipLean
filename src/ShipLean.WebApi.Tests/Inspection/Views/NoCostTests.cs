using System.Text.Json;
using ShipLean.Contracts.Inspection;
using ShipLean.WebApi.Tests.Taskboard;

namespace ShipLean.WebApi.Tests.Inspection.Views;

// D21: tokens only. No field name and no value or text in any K5 response or MCP reply mentions cost or price, content shown or not.
[TestFixture]
public class NoCostTests : InspectionViewsFixture
{
    private static readonly string[] Words = ["cost", "price", "usd", "dollar"];

    private static IEnumerable<string> Mentions(string body, string where)
        => Words.Where(word => body.Contains(word, StringComparison.OrdinalIgnoreCase)).Select(word => $"{where} mentions {word}");

    [TestCase(false)]
    [TestCase(true)]
    public async Task Responses_WhateverTheContentSetting_ShouldNotMentionCost(bool showContent)
    {
        using var saved = await SetContentAsync(showContent);
        saved.EnsureSuccessStatusCode();
        var page = await Api.GetAsync<RunPageDto>($"{RunsUrl}/{RunId}");
        var urls = new[] { RunsUrl, $"{RunsUrl}/{RunId}" }.Concat(page.Turns.SelectMany(t => t.Rows).Select(r => $"{RunsUrl}/{RunId}/rows/{r.Id}"));

        var findings = new List<string>();
        foreach (var url in urls)
        {
            using var response = await Api.GetAsync(url);
            findings.AddRange(Mentions(await response.Content.ReadAsStringAsync(), url));
        }
        var token = await ToolTokenAsync();
        foreach (var (tool, arguments) in new[] { ("list_runs", $$"""{"item":"{{Item}}"}"""), ("get_run", $$"""{"item":"{{Item}}","run":"{{RunId}}"}""") })
        {
            findings.AddRange(Mentions((await CallToolAsync(token, tool, arguments)).Body, tool));
        }
        var tools = (await PostAsync(token, """{"jsonrpc":"2.0","id":1,"method":"tools/list"}""")).Json().GetProperty("result").GetProperty("tools").EnumerateArray();
        foreach (var tool in tools.Where(t => t.GetProperty("name").GetString() is "list_runs" or "get_run" or "agent_setup"))
        {
            findings.AddRange(Mentions(tool.GetRawText(), "tools/list " + tool.GetProperty("name").GetString()));
        }

        findings.ShouldBeEmpty(string.Join("; ", findings));
    }
}
