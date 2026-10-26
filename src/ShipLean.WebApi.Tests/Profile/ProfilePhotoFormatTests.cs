using System.Net;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Profile;

// The header walker on files shaped like real encoders' output: segments before the frame header, and the hidden frame headers
// a crafted file puts where the walker would read them.
[TestFixture]
public class ProfilePhotoFormatTests : ApiFixture
{
    private static int _next;

    private async Task<TestUser> ArrangeUserAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        var number = Interlocked.Increment(ref _next);
        var id = await new AppUserBuilder().WithEmail($"photo-format-{number}@example.test").WithName($"Photo Format {number}").InsertAsync(connection);
        return TestUser.Of(id);
    }

    [TestCase(0x00)]
    [TestCase(0x02)]
    [TestCase(0xBF)]
    public async Task Profile_WhenJpegHidesAFrameHeaderBehindAnUnparsedMarker_ShouldBeValidation(byte marker)
    {
        var owner = await ArrangeUserAsync();

        using var response = await ProfilePhotos.PutAsync(Api.Http, owner, "image/jpeg", ProfilePhotos.JpegWithDecoy(marker));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [TestCase(4096, 4096, HttpStatusCode.OK)]
    [TestCase(4097, 1, HttpStatusCode.BadRequest)]
    [TestCase(1, 4097, HttpStatusCode.BadRequest)]
    public async Task Profile_WhenJpegHasJfifAndExifBeforeTheFrame_ShouldReadTheFrameSize(int width, int height, HttpStatusCode expected)
    {
        var owner = await ArrangeUserAsync();

        using var response = await ProfilePhotos.PutAsync(Api.Http, owner, "image/jpeg", ProfilePhotos.EncoderJpeg((ushort)width, (ushort)height));

        (await StatusAsync(response)).ShouldBe(expected);
    }

    [TestCase(1, 1, HttpStatusCode.OK)]
    [TestCase(5000, 1, HttpStatusCode.BadRequest)]
    public async Task Profile_WhenJpegHasATableSegmentBeforeTheFrame_ShouldNotReadTheTableAsTheFrame(int width, int height, HttpStatusCode expected)
    {
        var owner = await ArrangeUserAsync();

        using var response = await ProfilePhotos.PutAsync(Api.Http, owner, "image/jpeg", ProfilePhotos.JpegWithTableFirst((ushort)width, (ushort)height));

        (await StatusAsync(response)).ShouldBe(expected);
    }

    [TestCase(4096u, 4096u, HttpStatusCode.OK)]
    [TestCase(4097u, 1u, HttpStatusCode.BadRequest)]
    [TestCase(1u, 4097u, HttpStatusCode.BadRequest)]
    public async Task Profile_WhenWebPIsLossy_ShouldAcceptOnlyUpToTheLimit(uint width, uint height, HttpStatusCode expected)
    {
        var owner = await ArrangeUserAsync();

        using var response = await ProfilePhotos.PutAsync(Api.Http, owner, "image/webp", ProfilePhotos.WebPLossy(width, height));

        (await StatusAsync(response)).ShouldBe(expected);
    }

    [TestCase(4096u, 4096u, HttpStatusCode.OK)]
    [TestCase(4097u, 1u, HttpStatusCode.BadRequest)]
    [TestCase(1u, 4097u, HttpStatusCode.BadRequest)]
    public async Task Profile_WhenWebPIsLossless_ShouldAcceptOnlyUpToTheLimit(uint width, uint height, HttpStatusCode expected)
    {
        var owner = await ArrangeUserAsync();

        using var response = await ProfilePhotos.PutAsync(Api.Http, owner, "image/webp", ProfilePhotos.WebPLossless(width, height));

        (await StatusAsync(response)).ShouldBe(expected);
    }

    [Test]
    public async Task Profile_WhenLossyWebPHasNoFrameStartCode_ShouldBeValidation()
    {
        var owner = await ArrangeUserAsync();
        var bytes = ProfilePhotos.WebPLossy(1, 1);
        bytes[23] = 0;

        using var response = await ProfilePhotos.PutAsync(Api.Http, owner, "image/webp", bytes);

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    // The status, after checking that a refusal is a validation error.
    private static async Task<HttpStatusCode> StatusAsync(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
        }

        return response.StatusCode;
    }
}
