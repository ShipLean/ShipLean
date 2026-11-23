using System.Text.Json;
using Dapper;
using ShipLean.Contracts.Prompts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Prompts;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Prompts;

// The prompts tool over the real reads. Each test makes its versions on a prompt no other test in the class touches. Joel administers SHIP; Priya is a member there; PART is not Joel's.
[TestFixture]
public class PromptsToolTests : McpFixture
{
    protected override bool WithProbeTools => false;

    private async Task<JsonElement> ReadAsync(string arguments, TestUser? user = null)
    {
        var reply = await CallToolAsync(await TokenAsync(user ?? TestUser.Joel, ReadOnly), "prompts", arguments);
        return reply.Structured();
    }

    private static string ErrorCode(JsonElement structured) => structured.GetProperty("error").GetProperty("code").GetString()!;

    private static string? ErrorField(JsonElement structured) => structured.GetProperty("error").GetProperty("field").GetString();

    private Task<Guid> VersionAsync(PromptKey key, string text, bool inUse = false, int? number = null)
    {
        var builder = new PromptVersionBuilder(SeedIds.Projects.Ship, key).WithText(text);
        if (number is { } n)
        {
            builder.WithNumber(n);
        }

        return (inUse ? builder.InUse() : builder).InsertAsync(ConnectionString);
    }

    [Test]
    public async Task Prompts_WhenNoPrompt_ShouldListEveryPromptWithStatusAndInUseVersion()
    {
        await VersionAsync(PromptKey.Guide, "One.");
        await VersionAsync(PromptKey.Guide, "Two.", inUse: true);

        var result = await ReadAsync("""{"project":"SHIP"}""");

        var lines = result.GetProperty("prompts").EnumerateArray().ToList();
        lines.Count.ShouldBe(Enum.GetValues<PromptKey>().Length);
        var guide = lines.Single(l => l.GetProperty("key").GetString() == "guide");
        guide.GetProperty("status").GetString().ShouldBe("custom");
        guide.GetProperty("in_use_version").GetInt32().ShouldBe(2);
        guide.GetProperty("versions").GetInt32().ShouldBe(2);
        lines[0].GetProperty("stage").GetString().ShouldNotBeNullOrEmpty();
        result.GetProperty("custom_count").GetInt32().ShouldBeGreaterThanOrEqualTo(1);
        result.GetProperty("can_edit").GetBoolean().ShouldBeTrue();
    }

    [Test]
    public async Task Prompts_WhenPromptGiven_ShouldReturnTheInUseTextAndHistoryWithAgent()
    {
        await VersionAsync(PromptKey.Work, "Old work text.");
        var newest = await VersionAsync(PromptKey.Work, "Current work text.", inUse: true);
        await using (var connection = await OpenDatabaseAsync())
        {
            await connection.ExecuteAsync("UPDATE prompt_version SET agent_name = 'Claude Code (claude.ai)' WHERE id = @newest", new { newest });
        }

        var result = await ReadAsync("""{"project":"SHIP","prompt":"work"}""");

        result.GetProperty("key").GetString().ShouldBe("work");
        result.GetProperty("text_of").GetString().ShouldBe("v2");
        result.GetProperty("text").GetString().ShouldBe("Current work text.");
        result.GetProperty("in_use_version").GetInt32().ShouldBe(2);
        var versions = result.GetProperty("versions").EnumerateArray().ToList();
        versions.Select(v => v.GetProperty("number").GetInt32()).ShouldBe([2, 1]);
        versions[0].GetProperty("agent").GetString().ShouldBe("Claude Code (claude.ai)");
        versions[0].GetProperty("in_use").GetBoolean().ShouldBeTrue();
        versions[1].GetProperty("agent").ValueKind.ShouldBe(JsonValueKind.Null);
        versions[0].TryGetProperty("text", out _).ShouldBeFalse();
    }

    [Test]
    public async Task Prompts_WhenVersionGiven_ShouldReturnThatVersionsText()
    {
        await VersionAsync(PromptKey.Brief, "First brief text.");
        await VersionAsync(PromptKey.Brief, "Second brief text.", inUse: true);

        var result = await ReadAsync("""{"project":"SHIP","prompt":"brief","version":1}""");

        result.GetProperty("text_of").GetString().ShouldBe("v1");
        result.GetProperty("text").GetString().ShouldBe("First brief text.");
    }

    [Test]
    public async Task Prompts_WhenBuiltInGiven_ShouldReturnTheBuiltInText()
    {
        await VersionAsync(PromptKey.Fix, "A custom fix text.", inUse: true);

        var result = await ReadAsync("""{"project":"SHIP","prompt":"fix","version":"built-in"}""");

        result.GetProperty("text_of").GetString().ShouldBe("built-in");
        result.GetProperty("text").GetString().ShouldNotBeNullOrEmpty().ShouldNotBe("A custom fix text.");
    }

    [Test]
    public async Task Prompts_WhenMember_ShouldRead()
    {
        var result = await ReadAsync("""{"project":"SHIP","prompt":"codeReviewer"}""", TestUser.Priya);

        result.GetProperty("can_edit").GetBoolean().ShouldBeFalse();
        result.GetProperty("text_of").GetString().ShouldBe("built-in");
    }

    [Test]
    public async Task Prompts_WhenVersionUnknown_ShouldBeNotFound()
    {
        var result = await ReadAsync("""{"project":"SHIP","prompt":"guide","version":99}""");

        ErrorCode(result).ShouldBe("not_found");
    }

    [TestCase("0")]
    [TestCase("-1")]
    [TestCase("\"abc\"")]
    public async Task Prompts_WhenVersionInvalid_ShouldBeValidationOnVersion(string version)
    {
        var result = await ReadAsync($$"""{"project":"SHIP","prompt":"guide","version":{{version}}}""");

        ErrorCode(result).ShouldBe("validation");
        ErrorField(result).ShouldBe("version");
    }

    [Test]
    public async Task Prompts_WhenNotMember_ShouldBeNotFound()
    {
        var result = await ReadAsync("""{"project":"PART","prompt":"guide"}""");

        ErrorCode(result).ShouldBe("not_found");
    }
}
