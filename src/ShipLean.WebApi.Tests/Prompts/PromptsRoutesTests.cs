using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Prompts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Prompts;

// C1's routes over the 0.1 stand-ins: the shapes and the errors the controller itself owns. Joel administers SHIP.
[TestFixture]
public class PromptsRoutesTests : ApiFixture
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    private static string Route(string tail = "") => $"api/projects/{SeedIds.Projects.Ship}/prompts{tail}";

    [Test]
    public async Task Page_WhenOpened_ShouldReturnElevenPromptsInSevenStages()
    {
        using var response = await Api.GetAsync(Route());

        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        var page = JsonSerializer.Deserialize<PromptsPageDto>(text, Wire)!;
        page.Stages.Count.ShouldBe(7);
        page.Stages.SelectMany(s => s.Prompts).Count().ShouldBe(11);
        page.CanEdit.ShouldBeTrue();
    }

    [Test]
    public async Task Page_WhenUnknownPrompt_ShouldBeNotFound()
    {
        using var response = await Api.GetAsync(Route("/nonsense/history"));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ApiClient.ReadApiErrorAsync(response)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Assembled_WhenAgentUnknown_ShouldBeValidationOnAgent()
    {
        using var response = await Api.GetAsync(Route("/guide/assembled?agent=emacs"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var error = await ApiClient.ReadApiErrorAsync(response);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("agent");
    }

    [Test]
    public async Task Save_WhenStubbed_ShouldReturnTheSavedShape()
    {
        using var response = await Api.PostJsonAsync(Route("/guide/versions"), new { text = "Custom text.", note = "A change.", source = "editor" });

        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        JsonSerializer.Deserialize<PromptSavedDto>(text, Wire).ShouldBe(new PromptSavedDto(PromptKey.Guide, PromptStatus.Custom, 1));
    }
}
