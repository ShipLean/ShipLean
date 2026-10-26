using System.Net;
using System.Net.Http.Json;
using System.Text;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.WorkItemDetails.Attachments;
using ShipLean.Application.Storage;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Blobs;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Sprints.Api;

namespace ShipLean.WebApi.Tests.WorkItemDetails.Attachments;

// The attachment endpoints through the real app, with the size limit pinned at 1 KiB. Joel is the project admin and the
// default caller; Mei is a plain member; Kai is in no project.
[TestFixture]
public class AttachmentApiTests : ApiFixture
{
    private const int Limit = 1024;

    private static readonly string ItemUrl = $"api/work-items/{SeedIds.WorkItems.Ship90}/attachments";

    private static readonly string BlobRoot = Path.Combine(Path.GetTempPath(), $"shiplean-blobs-{Guid.NewGuid():N}");

    // The limit is pinned at 1 KiB and the files go to a folder of the test's own, not the app's data folder.
    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.AddSingleton(new AttachmentLimits(Limit));
        services.AddSingleton<IBlobStore>(new LocalDiskBlobStore(BlobRoot));
    };

    [OneTimeTearDown]
    public void DeleteBlobs() => Directory.Delete(BlobRoot, recursive: true);

    [Test]
    public async Task Upload_WhenTooLarge_ShouldBeValidationAndKeepNothing()
    {
        var before = (await Api.GetJsonAsync<List<AttachmentDto>>(ItemUrl)).Count;

        using var response = await AttachmentUploads.PostAsync(Client, ItemUrl, "big.bin", "application/octet-stream", new byte[Limit + 1]);

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        (await Api.GetJsonAsync<List<AttachmentDto>>(ItemUrl)).Count.ShouldBe(before);
    }

    [Test]
    public async Task Upload_WhenNameTooLong_ShouldBeValidationAndKeepNothing()
    {
        var before = (await Api.GetJsonAsync<List<AttachmentDto>>(ItemUrl)).Count;

        using var tooLong = await AttachmentUploads.PostAsync(Client, ItemUrl, new string('n', 256), "text/plain", [1]);
        var added = await AttachmentUploads.UploadAsync(Client, ItemUrl, new string('n', 255), "text/plain", [1]);

        var error = await ApiClient.ReadApiErrorAsync(tooLong, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        (await Api.GetJsonAsync<List<AttachmentDto>>(ItemUrl)).Count.ShouldBe(before + 1);
        added.Name.Length.ShouldBe(255);
    }

    [Test]
    public async Task Upload_WhenNoFile_ShouldBeValidation()
    {
        using var response = await Api.PostJsonAsync(ItemUrl, new { });

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Upload_WhenNotMember_ShouldBeRefusedBeforeTheBodyEnds()
    {
        var status = await AttachmentUploads.StatusLineWhileStalledAsync(Client.BaseAddress!, ItemUrl, TestUser.Kai, 64 * 1024, TimeSpan.FromSeconds(15));

        status.ShouldBe("HTTP/1.1 404 Not Found");
    }

    [Test]
    public async Task Upload_WhenMemberAndBodyIsOverTheLimit_ShouldBeRefusedBeforeTheBodyEnds()
    {
        var status = await AttachmentUploads.StatusLineWhileStalledAsync(Client.BaseAddress!, ItemUrl, null, 1024, TimeSpan.FromSeconds(15));

        status.ShouldBe("HTTP/1.1 413 Payload Too Large");
    }

    // The request cap is the limit plus 64 KiB of multipart framing: one byte over it is refused before any body is read.
    [Test]
    public async Task Upload_WhenDeclaredLengthIsOneOverTheCap_ShouldBeRefusedWithoutReadingTheBody()
    {
        var status = await AttachmentUploads.StatusLineWhileStalledAsync(
            Client.BaseAddress!, ItemUrl, null, 1024, TimeSpan.FromSeconds(15), declaredLength: Limit + 64 * 1024 + 1);

        status.ShouldBe("HTTP/1.1 413 Payload Too Large");
    }

    [Test]
    public async Task Upload_WhenDeclaredLengthIsAtTheCap_ShouldWaitForTheBody()
    {
        var status = await AttachmentUploads.StatusLineWhileStalledAsync(
            Client.BaseAddress!, ItemUrl, null, 1024, TimeSpan.FromSeconds(2), declaredLength: Limit + 64 * 1024);

        status.ShouldBeNull();
    }

    [TestCase("--BOUND\r\nContent-Disposition: form-data; name=\"file\"; filename=\"a.txt\"\r\nContent-Type: text/plain\r\n\r\nx")]
    [TestCase("garbage with no boundary at all")]
    [TestCase("--BOUND\r\nContent-Disposition: form-data; name=\"field\"\r\n\r\nx")]
    public async Task Upload_WhenMultipartIsMalformed_ShouldBeValidationAndKeepNothing(string body)
    {
        var before = (await Api.GetJsonAsync<List<AttachmentDto>>(ItemUrl)).Count;

        using var response = await AttachmentUploads.PostRawAsync(Client, ItemUrl, new StringContent(body, Encoding.ASCII));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
        (await Api.GetJsonAsync<List<AttachmentDto>>(ItemUrl)).Count.ShouldBe(before);
    }

    [Test]
    public async Task Upload_WhenPartHeaderIsOverSixteenKiB_ShouldBeValidationAndKeepNothing()
    {
        var before = (await Api.GetJsonAsync<List<AttachmentDto>>(ItemUrl)).Count;
        var body = $"--BOUND\r\nContent-Disposition: form-data; name=\"file\"; filename=\"a.txt\"\r\nX-Padding: {new string('a', 20 * 1024)}\r\n\r\nx\r\n--BOUND--\r\n";

        using var response = await AttachmentUploads.PostRawAsync(Client, ItemUrl, new StringContent(body, Encoding.ASCII));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
        (await Api.GetJsonAsync<List<AttachmentDto>>(ItemUrl)).Count.ShouldBe(before);
    }

    [Test]
    public async Task Upload_WhenPartNamedFileHasNoFileName_ShouldSkipItAndTakeTheNextFilePart()
    {
        var body = "--BOUND\r\nContent-Disposition: form-data; name=\"file\"\r\n\r\nnot a file\r\n"
            + "--BOUND\r\nContent-Disposition: form-data; name=\"file\"; filename=\"real.txt\"\r\nContent-Type: text/plain\r\n\r\nx\r\n--BOUND--\r\n";

        using var response = await AttachmentUploads.PostRawAsync(Client, ItemUrl, new StringContent(body, Encoding.ASCII));

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await response.Content.ReadFromJsonAsync<AttachmentDto>())!.Name.ShouldBe("real.txt");
    }

    [TestCase(70, HttpStatusCode.OK)]
    [TestCase(71, HttpStatusCode.BadRequest)]
    [TestCase(5000, HttpStatusCode.BadRequest)]
    public async Task Upload_WhenBoundaryLengthVaries_ShouldRefuseOnlyAboveSeventy(int length, HttpStatusCode expected)
    {
        var boundary = new string('b', length);
        var text = $"--{boundary}\r\nContent-Disposition: form-data; name=\"file\"; filename=\"b.txt\"\r\nContent-Type: text/plain\r\n\r\nx\r\n--{boundary}--\r\n";
        using var content = new StringContent(text, Encoding.ASCII);
        content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse($"multipart/form-data; boundary={boundary}");
        using var request = new HttpRequestMessage(HttpMethod.Post, ItemUrl) { Content = content };
        request.Headers.Add(ShipLean.Contracts.ClientHeader.Name, ApiClient.ClientId);

        using var response = await Client.SendAsync(request);

        response.StatusCode.ShouldBe(expected);
    }

    [TestCase("..\\..\\etc\\passwd", "passwd")]
    [TestCase("../../etc/shadow", "shadow")]
    [TestCase("C:\\Users\\me\\report.pdf", "report.pdf")]
    public async Task Upload_WhenNameHasPathSeparators_ShouldStoreOnlyTheLastSegment(string name, string expected)
    {
        using var response = await PostNamedAsync(name);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<AttachmentDto>())!.Name.ShouldBe(expected);
    }

    [TestCase("a\0b.txt")]
    [TestCase("a\r\nSet-Cookie: x=y.txt")]
    [TestCase("a\nb.txt")]
    [TestCase("a\u0007b.txt")]
    [TestCase("invoice\u202Efdp.exe")]
    [TestCase("a\u2066b.txt")]
    [TestCase("a\u2069b.txt")]
    public async Task Upload_WhenNameHasControlCharacters_ShouldBeValidationAndKeepNothing(string name)
    {
        var before = (await Api.GetJsonAsync<List<AttachmentDto>>(ItemUrl)).Count;

        using var response = await PostNamedAsync(name);

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
        (await Api.GetJsonAsync<List<AttachmentDto>>(ItemUrl)).Count.ShouldBe(before);
    }

    [Test]
    public async Task Upload_WhenContentTypeHasHugeParameters_ShouldKeepOnlyTheMediaType()
    {
        var huge = "text/plain; x=" + new string('a', 3000);

        var added = await AttachmentUploads.UploadAsync(Client, ItemUrl, "huge-type.txt", huge, [1]);

        added.ContentType.ShouldBe("text/plain");
    }

    // The name travels in a filename* parameter, which can carry control characters that a plain filename cannot.
    private Task<HttpResponseMessage> PostNamedAsync(string name)
    {
        var text = $"--BOUND\r\nContent-Disposition: form-data; name=\"file\"; filename*=UTF-8''{Uri.EscapeDataString(name)}\r\nContent-Type: text/plain\r\n\r\nx\r\n--BOUND--\r\n";
        return AttachmentUploads.PostRawAsync(Client, ItemUrl, new StringContent(text, Encoding.ASCII));
    }

    [Test]
    public async Task Attachment_WhenNotMember_ShouldBeNotFound()
    {
        var added = await AttachmentUploads.UploadAsync(Client, ItemUrl, "members.txt", "text/plain", [1, 2, 3]);
        var kai = Api.ActingAs(TestUser.Kai);

        using var list = await kai.GetAsync(ItemUrl);
        using var upload = await AttachmentUploads.PostAsync(Client, ItemUrl, "x.txt", "text/plain", [1], TestUser.Kai);

        foreach (var response in new[] { list, upload })
        {
            var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound);
            error.Code.ShouldBe(ApiErrorCodes.NotFound);
        }

        (await Api.GetJsonAsync<List<AttachmentDto>>(ItemUrl)).ShouldContain(a => a.Id == added.Id);
    }

    [Test]
    public async Task Add_WhenSaved_ShouldPublishAttachmentsChangedForTheWorkItem()
    {
        using var watch = Watch();

        await AttachmentUploads.UploadAsync(Client, ItemUrl, "heard.txt", "text/plain", [1]);

        (await watch.NextAsync<AttachmentsChanged>(e => e.WorkItemId == SeedIds.WorkItems.Ship90)).ProjectId.ShouldBe(SeedIds.Projects.Ship);
    }

    [Test]
    public async Task Upload_WhenRefused_ShouldPublishNothing()
    {
        using var watch = Watch();
        using var refused = await AttachmentUploads.PostAsync(Client, ItemUrl, "big.bin", "application/octet-stream", new byte[Limit + 1]);
        refused.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await AttachmentUploads.UploadAsync(Client, ItemUrl, "marker.txt", "text/plain", [1]);

        // The marker's own event is the first one after the refusal: nothing arrived in between.
        (await watch.NextAsync<AttachmentsChanged>(_ => true)).WorkItemId.ShouldBe(SeedIds.WorkItems.Ship90);
    }

    private ProjectEventWatch Watch() => new(Host.Services.GetRequiredService<IProjectEvents>(), SeedIds.Projects.Ship);
}
