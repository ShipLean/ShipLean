using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Prompts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Prompts;
using ShipLean.WebApi.Features.Prompts;

namespace ShipLean.WebApi.Tests.Prompts;

// A later ShipLean release changed the Report built-in: a project that kept its own text over the earlier one is flagged again.
[TestFixture]
public class PromptReadsBuiltInChangedTests : ApiFixture
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
        services.AddSingleton<IBuiltInPrompts>(new ChangedBuiltInPrompts(new BuiltInPrompts(), new Dictionary<PromptKey, string> { [PromptKey.Report] = "A newer report built-in." }));

    [Test]
    public async Task Page_WhenKeptMineAndTheBuiltInChangesAgain_ShouldFlagBuiltInUpdatedAgain()
    {
        var keptHash = new BuiltInPrompts().Hash(PromptKey.Report);
        await new PromptVersionBuilder(SeedIds.Projects.Ship, PromptKey.Report).WithText("Mine.").WithKeptBuiltInHash(keptHash).InUse().InsertAsync(ConnectionString);

        using var response = await Api.GetAsync($"api/projects/{SeedIds.Projects.Ship}/prompts");

        var text = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.ShouldBeTrue(text);
        var page = JsonSerializer.Deserialize<PromptsPageDto>(text, Wire)!;
        var report = page.Stages.SelectMany(s => s.Prompts).Single(p => p.Key == PromptKey.Report);
        report.Status.ShouldBe(PromptStatus.BuiltInUpdated);
        report.Update.ShouldNotBeNull();
        report.BuiltInText.ShouldBe("A newer report built-in.");
    }
}
