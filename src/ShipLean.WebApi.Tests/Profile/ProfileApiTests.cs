using System.Net;
using System.Net.Http.Json;
using Dapper;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Profile;
using ShipLean.Contracts.Users;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Profile;

// One seeded scenario; each test arranges a user of its own so the photos and names they change never meet.
[TestFixture]
public class ProfileApiTests : ApiFixture
{
    private const long BodyBytes = 16L * 1024 * 1024;

    private static int _next;

    private async Task<TestUser> ArrangeUserAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        var number = Interlocked.Increment(ref _next);
        var id = await new AppUserBuilder().WithEmail($"profile-{number}@example.test").WithName($"Profile Person {number}").InsertAsync(connection);
        return TestUser.Of(id);
    }

    private static string PhotoPath(MeDto me) => me.PhotoUrl ?? throw new InvalidOperationException("no photo");

    [Test]
    public async Task Profile_WhenPhotoUploaded_ShouldServeAtPhotoUrl()
    {
        var owner = await ArrangeUserAsync();
        var bytes = ProfilePhotos.Png(500);

        var me = await ProfilePhotos.UploadAsync(Api.Http, owner, "image/png", bytes);

        me.PhotoUrl.ShouldBe($"/api/users/{owner.Id}/photo?v=1");
        using var served = await Api.ActingAs(TestUser.Mei).GetAsync(PhotoPath(me));
        served.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await served.Content.ReadAsByteArrayAsync()).ShouldBe(bytes);
        served.Content.Headers.ContentType!.MediaType.ShouldBe("image/png");
        served.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
        served.Headers.GetValues("Content-Security-Policy").ShouldBe(["sandbox; frame-ancestors 'none'"]);
        served.Content.Headers.GetValues("Content-Disposition").ShouldBe(["inline; filename=\"photo\""]);
        served.Headers.GetValues("Cross-Origin-Resource-Policy").ShouldBe(["same-origin"]);
        served.Headers.CacheControl!.ToString().ShouldBe("max-age=31536000, private, immutable");
    }

    [Test]
    public async Task Profile_WhenPhotoVersionIsNotANumber_ShouldServeItUncached()
    {
        var owner = await ArrangeUserAsync();
        var me = await ProfilePhotos.UploadAsync(Api.Http, owner, "image/png", ProfilePhotos.Png());

        using var served = await Api.ActingAs(owner).GetAsync($"/api/users/{owner.Id}/photo?v=abc");

        served.StatusCode.ShouldBe(HttpStatusCode.OK);
        served.Headers.CacheControl!.ToString().ShouldBe("no-cache, private");
        me.PhotoUrl.ShouldNotBeNull();
    }

    [Test]
    public async Task Profile_WhenPhotoRequestedWithAnotherVersion_ShouldNotBeCachedForLong()
    {
        var owner = await ArrangeUserAsync();
        await ProfilePhotos.UploadAsync(Api.Http, owner, "image/png", ProfilePhotos.Png());

        using var stale = await Api.GetAsync($"/api/users/{owner.Id}/photo?v=0");
        using var missing = await Api.GetAsync($"/api/users/{owner.Id}/photo");

        stale.StatusCode.ShouldBe(HttpStatusCode.OK);
        stale.Headers.CacheControl!.ToString().ShouldBe("no-cache, private");
        missing.Headers.CacheControl!.ToString().ShouldBe("no-cache, private");
    }

    [Test]
    public async Task Profile_WhenPhotoTooLarge_ShouldBeValidation()
    {
        var owner = await ArrangeUserAsync();

        using var response = await ProfilePhotos.PutAsync(Api.Http, owner, "image/png", ProfilePhotos.Png(ProfilePhotos.MaxBytes + 1));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
        (await ProfileWire.GetMeAsync(Api, owner)).PhotoUrl.ShouldBeNull();
    }

    [Test]
    public async Task Profile_WhenPhotoIsExactlyTheLimit_ShouldBeAccepted()
    {
        var owner = await ArrangeUserAsync();

        var me = await ProfilePhotos.UploadAsync(Api.Http, owner, "image/png", ProfilePhotos.Png(ProfilePhotos.MaxBytes));

        me.PhotoUrl.ShouldNotBeNull();
    }

    [TestCase("png")]
    [TestCase("jpeg")]
    [TestCase("webp")]
    [TestCase("webp-lossy")]
    [TestCase("webp-lossless")]
    public async Task Profile_WhenPhotoDimensionsTooLarge_ShouldBeValidation(string format)
    {
        var owner = await ArrangeUserAsync();
        var (declared, bytes) = format switch
        {
            "png" => ("image/png", ProfilePhotos.Png(width: 5000)),
            "jpeg" => ("image/jpeg", ProfilePhotos.Jpeg(height: 5000)),
            "webp" => ("image/webp", ProfilePhotos.WebP(width: 5000)),
            "webp-lossy" => ("image/webp", ProfilePhotos.WebPLossy(height: 5000)),
            _ => ("image/webp", ProfilePhotos.WebPLossless(width: 5000)),
        };

        using var response = await ProfilePhotos.PutAsync(Api.Http, owner, declared, bytes);

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
        (await ProfileWire.GetMeAsync(Api, owner)).PhotoUrl.ShouldBeNull();
    }

    [TestCase("png")]
    [TestCase("jpeg")]
    [TestCase("webp")]
    public async Task Profile_WhenPhotoIsExactlyTheDimensionLimit_ShouldBeAccepted(string format)
    {
        var owner = await ArrangeUserAsync();
        var (declared, bytes) = format switch
        {
            "png" => ("image/png", ProfilePhotos.Png(width: 4096, height: 4096)),
            "jpeg" => ("image/jpeg", ProfilePhotos.Jpeg(width: 4096, height: 4096)),
            _ => ("image/webp", ProfilePhotos.WebP(width: 4096, height: 4096)),
        };

        var me = await ProfilePhotos.UploadAsync(Api.Http, owner, declared, bytes);

        me.PhotoUrl.ShouldNotBeNull();
    }

    [TestCase("png", 0u, 1u)]
    [TestCase("png", 1u, 0u)]
    [TestCase("jpeg", 0u, 1u)]
    [TestCase("jpeg", 1u, 0u)]
    public async Task Profile_WhenPhotoHasNoArea_ShouldBeValidation(string format, uint width, uint height)
    {
        var owner = await ArrangeUserAsync();
        var (declared, bytes) = format == "png"
            ? ("image/png", ProfilePhotos.Png(width: width, height: height))
            : ("image/jpeg", ProfilePhotos.Jpeg(width: (ushort)width, height: (ushort)height));

        using var response = await ProfilePhotos.PutAsync(Api.Http, owner, declared, bytes);

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [TestCase("image/png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })]
    [TestCase("image/jpeg", new byte[] { 0xFF, 0xD8, 0xFF })]
    [TestCase("image/webp", new byte[] { 0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50 })]
    public async Task Profile_WhenPhotoHeaderIsMissingOrTruncated_ShouldBeValidation(string declared, byte[] bytes)
    {
        var owner = await ArrangeUserAsync();

        using var response = await ProfilePhotos.PutAsync(Api.Http, owner, declared, bytes);

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
        (await ProfileWire.GetMeAsync(Api, owner)).PhotoUrl.ShouldBeNull();
    }

    [Test]
    public async Task Profile_WhenPngSignatureIsFollowedByHtml_ShouldBeValidation()
    {
        var owner = await ArrangeUserAsync();
        byte[] bytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, .. "<html><script>alert(1)</script></html>"u8];

        using var response = await ProfilePhotos.PutAsync(Api.Http, owner, "image/png", bytes);

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Profile_WhenExeIsDeclaredAsPng_ShouldBeValidation()
    {
        var owner = await ArrangeUserAsync();

        using var response = await ProfilePhotos.PutAsync(Api.Http, owner, "image/png", ProfilePhotos.Exe(), "holiday.png");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
        (await ProfileWire.GetMeAsync(Api, owner)).PhotoUrl.ShouldBeNull();
    }

    [Test]
    public async Task Profile_WhenImageBytesAreDeclaredAsAnotherType_ShouldBeValidation()
    {
        var owner = await ArrangeUserAsync();

        using var response = await ProfilePhotos.PutAsync(Api.Http, owner, "text/html", ProfilePhotos.Png());

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [TestCase("image/png", "png")]
    [TestCase("image/jpeg", "jpeg")]
    [TestCase("image/webp", "webp")]
    public async Task Profile_WhenEachFormatUploaded_ShouldBeServedWithTheTypeItsBytesProve(string declared, string format)
    {
        var owner = await ArrangeUserAsync();
        var bytes = format switch { "png" => ProfilePhotos.Png(), "jpeg" => ProfilePhotos.Jpeg(), _ => ProfilePhotos.WebP() };

        var me = await ProfilePhotos.UploadAsync(Api.Http, owner, declared, bytes);

        using var served = await Api.GetAsync(PhotoPath(me));
        served.Content.Headers.ContentType!.MediaType.ShouldBe(declared);
    }

    [Test]
    public async Task Profile_WhenJpegBytesAreDeclaredPng_ShouldServeTheProvedType()
    {
        var owner = await ArrangeUserAsync();

        var me = await ProfilePhotos.UploadAsync(Api.Http, owner, "image/png", ProfilePhotos.Jpeg());

        using var served = await Api.GetAsync(PhotoPath(me));
        served.Content.Headers.ContentType!.MediaType.ShouldBe("image/jpeg");
    }

    [Test]
    public async Task Profile_WhenPhotoReplaced_ShouldBumpTheVersionAndServeTheNewBytes()
    {
        var owner = await ArrangeUserAsync();
        var first = await ProfilePhotos.UploadAsync(Api.Http, owner, "image/png", ProfilePhotos.Png(100, 7));
        var replacement = ProfilePhotos.Jpeg(200, 8);

        var second = await ProfilePhotos.UploadAsync(Api.Http, owner, "image/jpeg", replacement);

        first.PhotoUrl.ShouldEndWith("?v=1");
        second.PhotoUrl.ShouldEndWith("?v=2");
        using var served = await Api.GetAsync(PhotoPath(second));
        (await served.Content.ReadAsByteArrayAsync()).ShouldBe(replacement);
    }

    [Test]
    public async Task Profile_WhenPhotoRemoved_ShouldClearTheReferenceAndBumpTheVersion()
    {
        var owner = await ArrangeUserAsync();
        var uploaded = await ProfilePhotos.UploadAsync(Api.Http, owner, "image/png", ProfilePhotos.Png());

        using var removal = await Api.ActingAs(owner).DeleteAsync("/api/me/photo");

        removal.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ProfileWire.ReadMeAsync(removal)).PhotoUrl.ShouldBeNull();
        using var gone = await Api.GetAsync(PhotoPath(uploaded));
        (await ApiClient.ReadApiErrorAsync(gone, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
        await using var connection = await OpenDatabaseAsync();
        (await connection.QuerySingleAsync<int>("SELECT photo_version FROM app_user WHERE id = @id", new { id = owner.Id })).ShouldBe(2);
        (await ProfilePhotos.UploadAsync(Api.Http, owner, "image/png", ProfilePhotos.Png())).PhotoUrl.ShouldEndWith("?v=3");
    }

    [Test]
    public async Task Profile_WhenPhotoRemovedWithNone_ShouldKeepTheVersion()
    {
        var owner = await ArrangeUserAsync();

        using var removal = await Api.ActingAs(owner).DeleteAsync("/api/me/photo");

        removal.StatusCode.ShouldBe(HttpStatusCode.OK);
        await using var connection = await OpenDatabaseAsync();
        (await connection.QuerySingleAsync<int>("SELECT photo_version FROM app_user WHERE id = @id", new { id = owner.Id })).ShouldBe(0);
    }

    [Test]
    public async Task Profile_WhenAnotherUsersPhotoRequestedByASignedInUser_ShouldServeItAndNotWhenAnonymous()
    {
        var owner = await ArrangeUserAsync();
        var me = await ProfilePhotos.UploadAsync(Api.Http, owner, "image/png", ProfilePhotos.Png());

        using var kai = await Api.ActingAs(TestUser.Kai).GetAsync(PhotoPath(me));
        using var anonymous = await Api.ActingAs(TestUser.Anonymous).GetAsync(PhotoPath(me));

        kai.StatusCode.ShouldBe(HttpStatusCode.OK);
        anonymous.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Profile_WhenUserHasNoPhotoOrDoesNotExist_ShouldBeNotFound()
    {
        var owner = await ArrangeUserAsync();

        using var none = await Api.GetAsync($"/api/users/{owner.Id}/photo?v=0");
        using var unknown = await Api.GetAsync($"/api/users/{Guid.NewGuid()}/photo?v=1");

        none.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Profile_WhenUploadHasNoFilePart_ShouldBeValidation()
    {
        var owner = await ArrangeUserAsync();
        using var request = new HttpRequestMessage(HttpMethod.Put, "api/me/photo") { Content = JsonContent.Create(new { }) };
        request.Headers.Add(ClientHeader.Name, ApiClient.ClientId);
        request.Headers.Add(TestAuthentication.UserHeader, owner.Id.ToString());

        using var response = await Api.Http.SendAsync(request);

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Profile_WhenFilePartHasNoClosingBoundary_ShouldBeValidation()
    {
        var owner = await ArrangeUserAsync();
        using var body = new ByteArrayContent([.. System.Text.Encoding.ASCII.GetBytes(ProfilePhotos.FilePartHead("B")), .. ProfilePhotos.Png()]);

        using var response = await ProfilePhotos.PutRawAsync(Api.Http, owner, "B", body);

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
        (await ProfileWire.GetMeAsync(Api, owner)).PhotoUrl.ShouldBeNull();
    }

    [Test]
    public async Task Profile_WhenPartHeaderIsOverSixteenKiB_ShouldBeValidation()
    {
        var owner = await ArrangeUserAsync();
        var head = $"--B\r\nContent-Disposition: form-data; name=\"file\"; filename=\"me.png\"\r\nX-Padding: {new string('a', 20 * 1024)}\r\n\r\n";
        using var body = new ByteArrayContent([.. System.Text.Encoding.ASCII.GetBytes(head), .. ProfilePhotos.Png(), .. System.Text.Encoding.ASCII.GetBytes("\r\n--B--\r\n")]);

        using var response = await ProfilePhotos.PutRawAsync(Api.Http, owner, "B", body);

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
        (await ProfileWire.GetMeAsync(Api, owner)).PhotoUrl.ShouldBeNull();
    }

    [Test]
    public async Task Profile_WhenPartNamedFileHasNoFileName_ShouldNotBeTakenAsThePhoto()
    {
        var owner = await ArrangeUserAsync();
        var head = "--B\r\nContent-Disposition: form-data; name=\"file\"\r\nContent-Type: image/png\r\n\r\n";
        using var body = new ByteArrayContent([.. System.Text.Encoding.ASCII.GetBytes(head), .. ProfilePhotos.Png(), .. System.Text.Encoding.ASCII.GetBytes("\r\n--B--\r\n")]);

        using var response = await ProfilePhotos.PutRawAsync(Api.Http, owner, "B", body);

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
        (await ProfileWire.GetMeAsync(Api, owner)).PhotoUrl.ShouldBeNull();
    }

    [TestCase(70, HttpStatusCode.OK)]
    [TestCase(71, HttpStatusCode.BadRequest)]
    [TestCase(5000, HttpStatusCode.BadRequest)]
    public async Task Profile_WhenBoundaryIsThisLong_ShouldBeReadOrValidation(int length, HttpStatusCode expected)
    {
        var owner = await ArrangeUserAsync();
        var boundary = new string('b', length);
        using var body = ProfilePhotos.Closed(boundary, ProfilePhotos.Png());

        using var response = await ProfilePhotos.PutRawAsync(Api.Http, owner, boundary, body);

        response.StatusCode.ShouldBe(expected);
        if (expected == HttpStatusCode.BadRequest)
        {
            (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
        }
    }

    // The cap is what stops a body that is mostly something else: 16 MiB of a non-file part before the file part must fail
    // when the cap (2 MiB and the framing) is reached, not after the whole body has been read. The socket buffers on this
    // host swallow about 3.5 MiB before the client feels the reset (3 MiB would be sent whole either way), so the bound is
    // half the body; without the cap the server reads all 16 MiB.
    [Test]
    public async Task Profile_WhenBodyIsMuchLargerThanThePhotoLimit_ShouldStopReadingAtTheCap()
    {
        var owner = await ArrangeUserAsync();
        using var body = new CountingBody(BodyBytes);
        string outcome;
        try
        {
            using var response = await ProfilePhotos.PutRawAsync(Api.Http, owner, "BOUND", body);
            outcome = ((int)response.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (HttpRequestException error)
        {
            outcome = error.Message;
        }

        body.Sent.ShouldBeLessThan(BodyBytes / 2, $"the request ended with {outcome} after {body.Sent} bytes");
        (await ProfileWire.GetMeAsync(Api, owner)).PhotoUrl.ShouldBeNull();
    }

    [Test]
    public async Task Profile_WhenNameChanged_ShouldUpdateMe()
    {
        var owner = await ArrangeUserAsync();

        using var response = await Api.ActingAs(owner).PutJsonAsync("/api/me/profile", new UpdateProfileRequest("  Ada Lovelace ", "#7B4FC9"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var returned = await ProfileWire.ReadMeAsync(response);
        var me = await ProfileWire.GetMeAsync(Api, owner);
        me.ShouldBe(returned);
        me.Name.ShouldBe("Ada Lovelace");
        me.Initials.ShouldBe("AL");
        me.AvatarColour.ShouldBe("#7b4fc9");
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("Ada\u0007Lovelace")]
    [TestCase("Ada\nLovelace")]
    [TestCase("\u202Eevil")]
    [TestCase("Ada\u2066Lovelace\u2069")]
    [TestCase("Joel\u200BDickson")]
    [TestCase("Joel\u200FDickson")]
    public async Task Profile_WhenNameBlankOrHasControlOrInvisibleCharacters_ShouldBeValidation(string name)
    {
        var owner = await ArrangeUserAsync();

        using var response = await Api.ActingAs(owner).PutJsonAsync("/api/me/profile", new UpdateProfileRequest(name, "#7b4fc9"));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Profile_WhenNameHasANoBreakSpaceAndAZeroWidthJoiner_ShouldBeAcceptedWithInitialsFromBothWords()
    {
        var owner = await ArrangeUserAsync();

        using var spaced = await Api.ActingAs(owner).PutJsonAsync("/api/me/profile", new UpdateProfileRequest("Ada\u00A0Lovelace", "#7b4fc9"));
        var spacedMe = await ProfileWire.ReadMeAsync(spaced);
        using var emoji = await Api.ActingAs(owner).PutJsonAsync("/api/me/profile", new UpdateProfileRequest("Dev \U0001F469\u200D\U0001F4BB", "#7b4fc9"));

        spacedMe.Initials.ShouldBe("AL");
        emoji.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ProfileWire.ReadMeAsync(emoji)).Name.ShouldBe("Dev \U0001F469\u200D\U0001F4BB");
    }

    [Test]
    public async Task Profile_WhenNameTooLong_ShouldBeValidation()
    {
        var owner = await ArrangeUserAsync();

        using var response = await Api.ActingAs(owner).PutJsonAsync("/api/me/profile", new UpdateProfileRequest(new string('a', 101), "#7b4fc9"));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [TestCase("#000000")]
    [TestCase("red")]
    [TestCase("")]
    public async Task Profile_WhenColourNotInThePalette_ShouldBeValidation(string colour)
    {
        var owner = await ArrangeUserAsync();

        using var response = await Api.ActingAs(owner).PutJsonAsync("/api/me/profile", new UpdateProfileRequest("Ada Lovelace", colour));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Profile_WhenNameChanged_ShouldNotChangeAnotherUser()
    {
        var owner = await ArrangeUserAsync();
        var other = await ArrangeUserAsync();
        var before = await ProfileWire.GetMeAsync(Api, other);

        using var response = await Api.ActingAs(owner).PutJsonAsync("/api/me/profile", new UpdateProfileRequest("Ada Lovelace", "#7b4fc9"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ProfileWire.GetMeAsync(Api, other)).ShouldBe(before);
    }

    [Test]
    public async Task Profile_WhenAnonymous_ShouldBeUnauthorizedBeforeTheBodyIsRead()
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, "api/me/photo")
        {
            Content = new MultipartFormDataContent { { new ByteArrayContent(ProfilePhotos.Png()), "file", "me.png" } },
        };
        request.Headers.Add(ClientHeader.Name, ApiClient.ClientId);
        request.Headers.Add(TestAuthentication.UserHeader, TestAuthentication.AnonymousValue);

        using var response = await Api.Http.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // A chunked multipart body: a non-file part of the given size, then a small file part. Counts what the server took.
    private sealed class CountingBody(long junkBytes) : HttpContent
    {
        public long Sent { get; private set; }

        protected override async Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context)
        {
            await WriteAsync(stream, System.Text.Encoding.ASCII.GetBytes("--BOUND\r\nContent-Disposition: form-data; name=\"junk\"\r\n\r\n"));
            var chunk = new byte[64 * 1024];
            for (var left = junkBytes; left > 0; left -= chunk.Length)
            {
                await WriteAsync(stream, chunk.AsMemory(0, (int)Math.Min(chunk.Length, left)));
            }

            await WriteAsync(stream, System.Text.Encoding.ASCII.GetBytes("\r\n" + ProfilePhotos.FilePartHead("BOUND")));
            await WriteAsync(stream, ProfilePhotos.Png());
            await WriteAsync(stream, System.Text.Encoding.ASCII.GetBytes("\r\n--BOUND--\r\n"));
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        private async Task WriteAsync(Stream stream, ReadOnlyMemory<byte> bytes)
        {
            await stream.WriteAsync(bytes);
            Sent += bytes.Length;
        }
    }
}
