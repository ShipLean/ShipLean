using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Prompts;
using ShipLean.Contracts.Usage;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Prompts;
using ShipLean.TestSupport.Usage;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Prompts;

// use_prompt_version over the real writes. Joel administers SHIP; Priya is a member there. Each test moves a prompt no other test in the class touches.
[TestFixture]
public class UsePromptVersionToolTests : McpFixture
{
    protected override bool WithProbeTools => false;

    private RecordingUsageEvents Recorder => (RecordingUsageEvents)Host.Services.GetRequiredService<IUsageEvents>();

    private async Task<JsonElement> UseAsync(string prompt, string version, TestUser? user = null)
    {
        var reply = await CallToolAsync(await TokenAsync(user ?? TestUser.Joel, ReadWrite), "use_prompt_version", $$"""{"project":"SHIP","prompt":"{{prompt}}","version":{{version}}}""");
        return reply.Structured();
    }

    private async Task<JsonElement> ReadAsync(string prompt)
    {
        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "prompts", $$"""{"project":"SHIP","prompt":"{{prompt}}"}""");
        return reply.Structured();
    }

    private Task<Guid> VersionAsync(PromptKey key, int number, bool inUse = false)
    {
        var builder = new PromptVersionBuilder(SeedIds.Projects.Ship, key).WithNumber(number).WithText($"Text {number}.");
        return (inUse ? builder.InUse() : builder).InsertAsync(ConnectionString);
    }

    private static string ErrorCode(JsonElement structured) => structured.GetProperty("error").GetProperty("code").GetString()!;

    [Test]
    public async Task UsePromptVersion_WhenNumber_ShouldMoveThePointer()
    {
        await VersionAsync(PromptKey.Work, 1);
        await VersionAsync(PromptKey.Work, 2, inUse: true);

        var result = await UseAsync("work", "1");

        result.GetProperty("in_use_version").GetInt32().ShouldBe(1);
        result.GetProperty("status").GetString().ShouldBe("custom");
        var read = await ReadAsync("work");
        read.GetProperty("in_use_version").GetInt32().ShouldBe(1);
        read.GetProperty("text").GetString().ShouldBe("Text 1.");
    }

    [Test]
    public async Task UsePromptVersion_WhenBuiltIn_ShouldUseTheBuiltIn()
    {
        await VersionAsync(PromptKey.Fix, 1, inUse: true);

        var result = await UseAsync("fix", "\"built-in\"");

        result.GetProperty("status").GetString().ShouldBe("usingBuiltIn");
        result.GetProperty("in_use_version").ValueKind.ShouldBe(JsonValueKind.Null);
        var read = await ReadAsync("fix");
        read.GetProperty("text_of").GetString().ShouldBe("built-in");
        read.GetProperty("versions").GetArrayLength().ShouldBe(1);
    }

    [Test]
    public async Task UsePromptVersion_WhenNotAdmin_ShouldRefuseAndChangeNothing()
    {
        await VersionAsync(PromptKey.Brief, 1);
        await VersionAsync(PromptKey.Brief, 2, inUse: true);

        var result = await UseAsync("brief", "1", TestUser.Priya);

        ErrorCode(result).ShouldBe("forbidden");
        (await ReadAsync("brief")).GetProperty("in_use_version").GetInt32().ShouldBe(2);
    }

    [Test]
    public async Task UsePromptVersion_WhenApplied_ShouldEmitTheWebEventOnMcpSurface()
    {
        await VersionAsync(PromptKey.Report, 1);
        Recorder.Clear();

        await UseAsync("report", "1");

        var tracked = Recorder.Events.Where(e => e.Source == "tool:use_prompt_version" && e.Name == UsageEventNames.PromptVersionRestored).ShouldHaveSingleItem();
        tracked.Surface.ShouldBe(UsageSurface.Mcp);
        tracked.Actor.Kind.ShouldBe(UsageActorKind.User);
        tracked.Actor.UserId.ShouldBe(SeedIds.Users.Joel);
        tracked.ProjectId.ShouldBe(SeedIds.Projects.Ship);
        tracked.Properties[UsageProps.Prompt].ShouldBe("report");
    }

    [Test]
    public async Task UsePromptVersion_WhenBuiltIn_ShouldEmitTheBuiltInEventOnMcpSurface()
    {
        await VersionAsync(PromptKey.Lead, 1, inUse: true);
        Recorder.Clear();

        await UseAsync("lead", "\"built-in\"");

        var tracked = Recorder.Events.Where(e => e.Source == "tool:use_prompt_version" && e.Name == UsageEventNames.PromptBuiltInUsed).ShouldHaveSingleItem();
        tracked.Surface.ShouldBe(UsageSurface.Mcp);
        tracked.ProjectId.ShouldBe(SeedIds.Projects.Ship);
        tracked.Properties[UsageProps.Prompt].ShouldBe("lead");
    }

    [Test]
    public async Task UsePromptVersion_WhenCallFails_ShouldEmitNoEventOfItsOwn()
    {
        Recorder.Clear();

        var result = await UseAsync("retro", "\"built-in\"");

        ErrorCode(result).ShouldBe("conflict");
        Recorder.Events.Where(e => e.Source == "tool:use_prompt_version").ShouldBeEmpty();
    }

    [Test]
    public async Task UsePromptVersion_WhenAlreadyBuiltIn_ShouldConflict()
    {
        var result = await UseAsync("planReviewer", "\"built-in\"");

        ErrorCode(result).ShouldBe("conflict");
    }

    [Test]
    public async Task UsePromptVersion_WhenVersionUnknown_ShouldBeNotFound()
    {
        var result = await UseAsync("codeReviewer", "42");

        ErrorCode(result).ShouldBe("not_found");
    }

    [TestCase("0")]
    [TestCase("-1")]
    [TestCase("\"abc\"")]
    public async Task UsePromptVersion_WhenVersionInvalid_ShouldBeValidationOnVersion(string version)
    {
        var result = await UseAsync("guide", version);

        ErrorCode(result).ShouldBe("validation");
        result.GetProperty("error").GetProperty("field").GetString().ShouldBe("version");
    }
}
