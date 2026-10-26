using System.Net;
using System.Net.Http.Json;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.WorkItemDetails.Attachments;

// The check endpoint's shape and access. Which outcome each file gets is AttachmentCheckContract's job.
[TestFixture]
public class AttachmentCheckApiTests : ApiFixture
{
    private static readonly string CheckUrl = $"api/work-items/{SeedIds.WorkItems.Ship90}/attachments/check";

    [Test]
    public async Task Check_WhenMember_ShouldAnswerOneOutcomePerFileInOrder()
    {
        var request = new AttachmentCheckRequest([new("b.png", 10, "image/png"), new("a.md", 20, "text/markdown")]);

        using var response = await Api.PostJsonAsync(CheckUrl, request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<AttachmentCheckDto>();
        result.ShouldNotBeNull().Files.Select(f => f.Name).ShouldBe(["b.png", "a.md"]);
    }

    [Test]
    public async Task Check_WhenNotMember_ShouldReturnNotFound()
    {
        using var response = await Api.ActingAs(TestUser.Kai).PostJsonAsync(CheckUrl, new AttachmentCheckRequest([new("a.png", 1, "image/png")]));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Check_WhenNameExists_ShouldReturnReplace()
    {
        await AttachmentUploads.UploadAsync(Client, $"api/work-items/{SeedIds.WorkItems.Ship90}/attachments", "twice.png", "image/png", [1]);

        using var response = await Api.PostJsonAsync(CheckUrl, new AttachmentCheckRequest([new("dir/twice.png", 5, "image/png"), new("fresh.png", 5, "image/png")]));

        var files = (await response.Content.ReadFromJsonAsync<AttachmentCheckDto>())!.Files;
        files.Select(f => f.Outcome).ShouldBe([AttachmentOutcome.Replace, AttachmentOutcome.Add]);
    }

    [Test]
    public async Task Check_WhenTooLarge_ShouldReturnTooLarge()
    {
        using var response = await Api.PostJsonAsync(CheckUrl, new AttachmentCheckRequest([new("huge.mov", 26L * 1024 * 1024, "video/quicktime")]));

        var file = (await response.Content.ReadFromJsonAsync<AttachmentCheckDto>())!.Files.Single();
        file.Outcome.ShouldBe(AttachmentOutcome.TooLarge);
        file.Message.ShouldContain("25 MB");
    }

    [Test]
    public async Task Check_WhenNameMissingOrSizeNegative_ShouldReturnValidationOnFiles()
    {
        foreach (var body in new[] { """{"files":[{"sizeBytes":1,"contentType":"a/b"}]}""", """{"files":[{"name":"a.png","sizeBytes":-1,"contentType":"a/b"}]}""" })
        {
            using var response = await Api.PostJsonAsync(CheckUrl, System.Text.Json.JsonDocument.Parse(body).RootElement);

            var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
            error.Code.ShouldBe(ApiErrorCodes.Validation);
            error.Field.ShouldBe("files");
        }
    }
}
