using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Inbox;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Inbox;

// The four Inbox routes through the real app: the reads answer in their final shapes for a member and not_found for anyone
// else, and the mention writes answer not_found for an id that is no one's mention. Nothing here depends on what the reads hold.
[TestFixture]
public class InboxRouteTests : ApiFixture
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonSerializer.Deserialize<T>(text, Wire)!;
    }

    [Test]
    public async Task InboxRoutes_WhenStubbed_ShouldReturnFinalShape()
    {
        var project = SeedIds.Projects.Ship;

        using var inboxResponse = await Api.ActingAs(TestUser.Tom).GetAsync($"api/projects/{project}/inbox");
        var inbox = await ReadAsync<InboxDto>(inboxResponse);
        using var answeredResponse = await Api.ActingAs(TestUser.Tom).GetAsync($"api/projects/{project}/inbox/answered");
        var answered = await ReadAsync<AnsweredDto>(answeredResponse);

        inbox.ProjectId.ShouldBe(project);
        inbox.LoadedAtLabel.ShouldNotBeNullOrWhiteSpace();
        inbox.Items.ShouldNotBeNull();
        inbox.OtherProjects.ShouldNotBeNull();
        answered.Rows.ShouldNotBeNull();
    }

    [TestCase("inbox")]
    [TestCase("inbox/answered")]
    public async Task InboxRoutes_WhenNotAMember_ShouldReturnNotFound(string route)
    {
        using var response = await Api.ActingAs(TestUser.Kai).GetAsync($"api/projects/{SeedIds.Projects.Ship}/{route}");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task MentionSeen_WhenUnknownId_ShouldReturnNotFound()
    {
        // Seen takes no body.
        using var response = await Api.ActingAs(TestUser.Tom).PostJsonAsync($"api/mentions/{Guid.NewGuid()}/seen");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task MentionReply_WhenUnknownId_ShouldReturnNotFound()
    {
        using var response = await Api.ActingAs(TestUser.Tom)
            .PostJsonAsync($"api/mentions/{Guid.NewGuid()}/reply", new MentionReplyRequest("Thanks, looking now."));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [TestCase("")]
    [TestCase("   ")]
    public async Task MentionReply_WhenBodyEmpty_ShouldReturnValidationOnBody(string body)
    {
        using var response = await Api.ActingAs(TestUser.Tom)
            .PostJsonAsync($"api/mentions/{Guid.NewGuid()}/reply", new MentionReplyRequest(body));

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("body");
    }
}
