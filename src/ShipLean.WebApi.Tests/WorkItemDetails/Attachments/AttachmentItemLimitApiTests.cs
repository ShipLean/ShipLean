using ShipLean.Application.Storage;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.WorkItemDetails.Attachments;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.WorkItemDetails.Attachments;

// The per-item limit, pinned small (each test has an item of its own): 1000-byte files, 1500 bytes for the whole item.
[TestFixture]
public class AttachmentItemLimitApiTests : ApiFixture
{
    private static readonly string FullUrl = $"api/work-items/{SeedIds.WorkItems.Ship92}/attachments";

    private static readonly string SwapUrl = $"api/work-items/{SeedIds.WorkItems.Ship91}/attachments";

    protected override Action<IServiceCollection>? ConfigureTestServices => services => services.AddSingleton(new AttachmentLimits(1000, 1500));

    [Test]
    public async Task Upload_WhenItemOver250Mb_ShouldReturnValidation()
    {
        await AttachmentUploads.UploadAsync(Client, FullUrl, "first.bin", "application/octet-stream", new byte[1000]);

        using var response = await AttachmentUploads.PostAsync(Client, FullUrl, "second.bin", "application/octet-stream", new byte[1000]);

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Error.ShouldContain("work item can hold at most");
    }

    [Test]
    public async Task Upload_WhenReplacingAFileThatWouldFreeTheRoom_ShouldStoreIt()
    {
        await AttachmentUploads.UploadAsync(Client, SwapUrl, "swap.bin", "application/octet-stream", new byte[1000]);
        var swapped = await AttachmentUploads.UploadAsync(Client, SwapUrl, "swap.bin", "application/octet-stream", new byte[900]);

        swapped.SizeBytes.ShouldBe(900);
        swapped.ReplacedCount.ShouldBe(1);
    }
}
