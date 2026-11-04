using System.Net;
using System.Net.Http.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Application.Features.Demos.Upload;
using ShipLean.Application.Markdown;
using ShipLean.Application.Storage;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Demos;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Demos;

// K8's protocol up to the handoff, over the real route, tokens and access check, with no sign-in at all: the token is the whole credential. IDemoDelivery
// is a recording double (U2's real one is not in this wave), so what each test sees is what the upload handed over, or that it handed over nothing.
[TestFixture]
public class DemoUploadApiTests : ApiFixture
{
    private const string NotValid = "That upload link is not valid.";
    private const long FileLimit = 1024 * 1024;
    private readonly RecordingDemoDelivery _delivery = new();
    private string _featureKey = string.Empty;

    protected override bool UseTestSignIn => false;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.RemoveAll<AttachmentLimits>();
        services.AddSingleton(new AttachmentLimits(FileLimit));
        services.RemoveAll<IDemoDelivery>();
        services.AddSingleton(_delivery);
        services.AddScoped<IDemoDelivery, ActingDemoDelivery>();
    };

    [OneTimeSetUp]
    public async Task ArrangeFeature()
    {
        await using var database = await OpenDatabaseAsync();
        var feature = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithTitle("Checkout v2").InsertAsync(database);
        _featureKey = await database.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @feature", new { feature });
    }

    [SetUp]
    public void ResetDelivery() => _delivery.Reset();

    private string Chapters(string? featureKey = null)
        => $$"""{"durationSeconds": 142.6, "chapters": [{"title": "Intro", "startSeconds": 0}, {"title": "Checkout v2", "startSeconds": 15.2, "featureKey": "{{featureKey ?? _featureKey}}"}]}""";

    private async Task<DemoUploadArrange.Arranged> NewAsync(Guid? userId = null, Guid? authorizationId = null)
    {
        await using var database = await OpenDatabaseAsync();
        return await DemoUploadArrange.NewAsync(database, Host.Services, userId ?? SeedIds.Users.Joel, authorizationId);
    }

    private Task<HttpResponseMessage> PostValidAsync(DemoUploadArrange.Arranged upload)
        => Client.PostAsync(upload.Path, DemoUploadArrange.Form(Chapters(), "# The demo", DemoUploadArrange.Mp4()));

    private static async Task ExpectNotValidAsync(HttpResponseMessage response)
    {
        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound);
        error.Code.ShouldBe(ApiErrorCodes.NotFound);
        error.Error.ShouldBe(NotValid);
    }

    private static async Task<ApiError> ExpectValidationAsync(HttpResponseMessage response)
    {
        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        return error;
    }

    private async Task RunAsync(string sql, object? parameters = null)
    {
        await using var database = await OpenDatabaseAsync();
        await database.ExecuteAsync(sql, parameters);
    }

    private async Task<T> ScopedAsync<T>(Func<IServiceProvider, Task<T>> work)
    {
        await using var scope = Host.Services.CreateAsyncScope();
        return await work(scope.ServiceProvider);
    }

    [Test]
    public async Task Upload_WhenValid_ShouldHandDeliveryThePartsAndReturnItsResult()
    {
        var upload = await NewAsync();
        var video = DemoUploadArrange.Mp4();

        using var response = await PostValidAsync(upload);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = (await response.Content.ReadFromJsonAsync<DemoUploadedDto>())!;
        body.SprintName.ShouldBe("Sprint 23");
        body.VideoBytes.ShouldBe(video.Length);
        body.Chapters.ShouldBe(2);
        body.OpenUrl.ShouldBe("https://planner.example.com/open");
        var delivered = _delivery.Calls.Single();
        delivered.Video.ShouldBe(video);
        delivered.Script.ShouldBe("# The demo");
        delivered.Token.ShouldBe(upload.Token);
        delivered.Chapters.DurationSeconds.ShouldBe(142.6m);
        delivered.Chapters.Chapters.Select(c => c.Title).ShouldBe(["Intro", "Checkout v2"]);
        delivered.Chapters.Chapters[1].FeatureKey.ShouldBe(_featureKey);
    }

    [Test]
    public async Task Upload_WhenValid_ShouldActAsTheTokensUserNotTheCaller()
    {
        var upload = await NewAsync(SeedIds.Users.Priya);

        using var response = await PostValidAsync(upload);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        _delivery.ActingUserId.ShouldBe(SeedIds.Users.Priya);
    }

    [Test]
    public async Task Upload_WhenOverTheFileLimitButUnderTheDemoLimit_ShouldReachDelivery()
    {
        var upload = await NewAsync();
        var video = DemoUploadArrange.Mp4OfSize(31 * 1024 * 1024);

        using var response = await Client.PostAsync(upload.Path, DemoUploadArrange.Form(Chapters(), "# The demo", video));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        _delivery.Calls.Single().Video.Length.ShouldBe(video.Length);
    }

    [Test]
    public async Task Upload_WhenSecondTime_ShouldBeNotFound()
    {
        var upload = await NewAsync();
        (await PostValidAsync(upload)).StatusCode.ShouldBe(HttpStatusCode.OK);

        using var second = await PostValidAsync(upload);

        await ExpectNotValidAsync(second);
        _delivery.Calls.Count.ShouldBe(1);
    }

    [Test]
    [CancelAfter(60_000)]
    public async Task Upload_WhenConcurrent_ShouldDeliverOnce()
    {
        var upload = await NewAsync();
        await using var holder = await OpenDatabaseAsync();
        await using var lockTransaction = await holder.BeginTransactionAsync();
        await holder.ExecuteAsync("SELECT 1 FROM transfer_token WHERE work_item_id = @itemId FOR UPDATE", new { itemId = upload.ItemId }, lockTransaction);

        // Both requests find the live token (a plain read, which the row lock does not block), then wait in ClaimAsync's UPDATE behind the held lock.
        var first = PostValidAsync(upload);
        var second = PostValidAsync(upload);
        await WaitForClaimsBlockedAsync(2);
        await lockTransaction.RollbackAsync();
        using var firstResponse = await first;
        using var secondResponse = await second;

        var responses = new[] { firstResponse, secondResponse };
        responses.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBe(1);
        await ExpectNotValidAsync(responses.Single(r => r.StatusCode != HttpStatusCode.OK));
        _delivery.Entered.ShouldBe(1);
        _delivery.Calls.Count.ShouldBe(1);
    }

    private async Task WaitForClaimsBlockedAsync(int expected)
    {
        await using var watcher = await OpenDatabaseAsync();
        using var tick = new PeriodicTimer(TimeSpan.FromMilliseconds(50));
        var blocked = 0;
        for (var attempt = 0; attempt < 600 && blocked < expected; attempt++)
        {
            blocked = await watcher.ExecuteScalarAsync<int>(
                "SELECT count(*)::int FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock' AND query LIKE 'UPDATE transfer_token SET used_at%'");
            if (blocked < expected)
            {
                await tick.WaitForNextTickAsync();
            }
        }

        blocked.ShouldBe(expected);
    }

    // A separate, valid token that the same POST accepts: a refusal test shows its own check did the refusing, not a route that answers 404 to anything.
    private async Task ExpectControlAcceptedAsync()
    {
        var control = await NewAsync();
        using var response = await PostValidAsync(control);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task Upload_WhenExpired_ShouldBeNotFound()
    {
        var upload = await NewAsync();
        await RunAsync("UPDATE transfer_token SET expires_at = now() - interval '1 minute' WHERE work_item_id = @itemId", new { itemId = upload.ItemId });

        using var response = await PostValidAsync(upload);

        await ExpectControlAcceptedAsync();
        await ExpectNotValidAsync(response);
        _delivery.Entered.ShouldBe(1);
    }

    [Test]
    public async Task Upload_WhenTokenRevokedByRetry_ShouldBeNotFound()
    {
        var upload = await NewAsync();
        await ScopedAsync(async services =>
        {
            await services.GetRequiredService<ITransferTokens>().RevokeAsync(TransferKind.Demo, upload.ItemId);
            return 0;
        });

        using var response = await PostValidAsync(upload);

        await ExpectControlAcceptedAsync();
        await ExpectNotValidAsync(response);
        _delivery.Entered.ShouldBe(1);
    }

    [Test]
    public async Task Upload_WhenUploadKindToken_ShouldBeNotFound()
    {
        var upload = await NewAsync();
        var issued = await ScopedAsync(services => services.GetRequiredService<ITransferTokens>().IssueAsync(
            new NewTransfer(TransferKind.Upload, upload.ItemId, null, "x.bin", "application/octet-stream", SeedIds.Users.Joel, null, DemoUploadArrange.AgentStamp)));

        using var response = await Client.PostAsync($"api/uploads/demo?token={Uri.EscapeDataString(issued.Token)}", DemoUploadArrange.Form(Chapters(), "# The demo", DemoUploadArrange.Mp4()));

        await ExpectControlAcceptedAsync();
        await ExpectNotValidAsync(response);
        _delivery.Entered.ShouldBe(1);
    }

    [Test]
    public async Task DemoToken_WhenUsedOnPutUploads_ShouldBeNotFound()
    {
        var upload = await NewAsync();

        using var put = await Client.PutAsync($"api/uploads/{Uri.EscapeDataString(upload.Token)}", new ByteArrayContent([1, 2, 3]));

        await ExpectNotValidAsync(put);
        (await RunQueryAsync<int?>("SELECT count(*)::int FROM transfer_token WHERE work_item_id = @itemId AND used_at IS NULL", new { itemId = upload.ItemId })).ShouldBe(1);
        (await PostValidAsync(upload)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task<T?> RunQueryAsync<T>(string sql, object? parameters = null)
    {
        await using var database = await OpenDatabaseAsync();
        return await database.ExecuteScalarAsync<T>(sql, parameters);
    }

    [Test]
    public async Task Upload_WhenNotMp4_ShouldBeValidationAndRetryable()
    {
        var upload = await NewAsync();

        using var response = await Client.PostAsync(upload.Path, DemoUploadArrange.Form(Chapters(), "# The demo", "this is not a video at all"u8.ToArray()));

        var error = await ExpectValidationAsync(response);
        error.Error.ShouldBe("The video must be an MP4.");
        _delivery.Entered.ShouldBe(0);
        (await PostValidAsync(upload)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task Upload_WhenVideoShorterThanItsHeader_ShouldBeValidation()
    {
        var upload = await NewAsync();

        using var response = await Client.PostAsync(upload.Path, DemoUploadArrange.Form(Chapters(), "# The demo", "ftyp"u8.ToArray()));

        (await ExpectValidationAsync(response)).Error.ShouldBe("The video must be an MP4.");
        _delivery.Entered.ShouldBe(0);
    }

    [Test]
    public async Task Upload_WhenChaptersInvalid_ShouldBeValidationAndNeverReachDelivery()
    {
        var upload = await NewAsync();

        using var response = await Client.PostAsync(upload.Path, DemoUploadArrange.Form(
            """{"durationSeconds": 142.6, "chapters": [{"title": "Intro", "startSeconds": 5}]}""", "# The demo", DemoUploadArrange.Mp4()));

        var error = await ExpectValidationAsync(response);
        error.Field.ShouldBe("chapters");
        _delivery.Entered.ShouldBe(0);
        (await PostValidAsync(upload)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task Upload_WhenAChapterNamesAnItemOfAnotherProject_ShouldBeValidationAndNeverReachDelivery()
    {
        var upload = await NewAsync();
        var foreign = await RunQueryAsync<string>("SELECT key FROM work_item WHERE project_id <> @project ORDER BY key LIMIT 1", new { project = SeedIds.Projects.Ship });

        using var response = await Client.PostAsync(upload.Path, DemoUploadArrange.Form(Chapters(foreign), "# The demo", DemoUploadArrange.Mp4()));

        (await ExpectValidationAsync(response)).Error.ShouldContain(foreign!);
        _delivery.Entered.ShouldBe(0);
    }

    [Test]
    public async Task Upload_WhenAChapterNamesARemovedItem_ShouldBeValidationAndNeverReachDelivery()
    {
        var upload = await NewAsync();
        string removedKey;
        await using (var database = await OpenDatabaseAsync())
        {
            var removed = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithTitle("Dropped idea").WithStateNamed("Removed").InsertAsync(database);
            removedKey = await database.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @removed", new { removed });
        }

        using var response = await Client.PostAsync(upload.Path, DemoUploadArrange.Form(Chapters(removedKey), "# The demo", DemoUploadArrange.Mp4()));

        (await ExpectValidationAsync(response)).Error.ShouldContain(removedKey);
        _delivery.Entered.ShouldBe(0);
        (await PostValidAsync(upload)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task Upload_WhenAChapterNamesNoItem_ShouldBeValidationAndNeverReachDelivery()
    {
        var upload = await NewAsync();

        using var response = await Client.PostAsync(upload.Path, DemoUploadArrange.Form(Chapters("SHIP-99999999"), "# The demo", DemoUploadArrange.Mp4()));

        (await ExpectValidationAsync(response)).Error.ShouldContain("SHIP-99999999");
        _delivery.Entered.ShouldBe(0);
    }

    [Test]
    public async Task Upload_WhenScriptTooLong_ShouldBeValidation()
    {
        var upload = await NewAsync();

        using var response = await Client.PostAsync(upload.Path, DemoUploadArrange.Form(Chapters(), new string('a', MarkdownRenderer.MaxLength + 1), DemoUploadArrange.Mp4()));

        var error = await ExpectValidationAsync(response);
        error.Field.ShouldBe("script");
        _delivery.Entered.ShouldBe(0);
    }

    [Test]
    public async Task Upload_WhenScriptAtTheLimit_ShouldReachDelivery()
    {
        var upload = await NewAsync();

        using var response = await Client.PostAsync(upload.Path, DemoUploadArrange.Form(Chapters(), new string('a', MarkdownRenderer.MaxLength), DemoUploadArrange.Mp4()));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        _delivery.Calls.Single().Script.Length.ShouldBe(MarkdownRenderer.MaxLength);
    }

    [Test]
    public async Task Upload_WhenScriptBlank_ShouldBeValidation()
    {
        var upload = await NewAsync();

        using var response = await Client.PostAsync(upload.Path, DemoUploadArrange.Form(Chapters(), "  \n ", DemoUploadArrange.Mp4()));

        (await ExpectValidationAsync(response)).Field.ShouldBe("script");
        _delivery.Entered.ShouldBe(0);
    }

    [Test]
    public async Task Upload_WhenPartsOutOfOrder_ShouldBeValidation()
    {
        var upload = await NewAsync();

        using var response = await Client.PostAsync(upload.Path, DemoUploadArrange.Form(Chapters(), "# The demo", DemoUploadArrange.Mp4(), "script", "chapters", "video"));

        var error = await ExpectValidationAsync(response);
        error.Field.ShouldBe("chapters");
        error.Error.ShouldContain("Expected the chapters part next but found script");
        _delivery.Entered.ShouldBe(0);
    }

    [Test]
    public async Task Upload_WhenVideoMissing_ShouldBeValidationNamingIt()
    {
        var upload = await NewAsync();

        using var response = await Client.PostAsync(upload.Path, DemoUploadArrange.Form(Chapters(), "# The demo", DemoUploadArrange.Mp4(), "chapters", "script"));

        (await ExpectValidationAsync(response)).Field.ShouldBe("video");
        _delivery.Entered.ShouldBe(0);
    }

    [Test]
    public async Task Upload_WhenNotMultipart_ShouldBeValidationAndRetryable()
    {
        var upload = await NewAsync();

        using var response = await Client.PostAsync(upload.Path, new ByteArrayContent([1, 2, 3]));

        await ExpectValidationAsync(response);
        (await PostValidAsync(upload)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task Upload_WhenDeliveryRefuses_ShouldAnswerItsValidationAndReleaseTheToken()
    {
        var upload = await NewAsync();
        _delivery.Before = _ => throw ApiException.Validation("That video is too large for this item.");

        using var refused = await PostValidAsync(upload);

        (await ExpectValidationAsync(refused)).Error.ShouldBe("That video is too large for this item.");
        _delivery.Before = null;
        (await PostValidAsync(upload)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task Upload_WhenDeliveryFails_ShouldReleaseTheToken()
    {
        var upload = await NewAsync();
        _delivery.Before = _ => throw new InvalidOperationException("The blob store is down.");

        using var failed = await PostValidAsync(upload);

        failed.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        _delivery.Before = null;
        (await PostValidAsync(upload)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task Upload_WhenRevokedAfterClaimThenFails_ShouldStayDead()
    {
        var upload = await NewAsync();
        _delivery.Before = async _ =>
        {
            await ScopedAsync(async services =>
            {
                await services.GetRequiredService<ITransferTokens>().RevokeAsync(TransferKind.Demo, upload.ItemId);
                return 0;
            });
            throw ApiException.Validation("A part failed.");
        };

        using var failed = await PostValidAsync(upload);

        await ExpectValidationAsync(failed);
        _delivery.Before = null;
        using var again = await PostValidAsync(upload);
        await ExpectNotValidAsync(again);
        _delivery.Calls.ShouldBeEmpty();
    }

    [Test]
    public async Task Upload_WhenDeliveryWasSuperseded_ShouldBeTheSameNotFound()
    {
        var upload = await NewAsync();
        _delivery.Before = _ => throw ApiException.NotFound("This delivery was replaced by a retry.");

        using var response = await PostValidAsync(upload);

        await ExpectNotValidAsync(response);
        _delivery.Entered.ShouldBe(1);
    }

    [Test]
    public async Task Upload_WhenUserDisabled_ShouldBeNotFound()
    {
        var upload = await NewAsync(SeedIds.Users.Priya);
        await RunAsync("UPDATE app_user SET disabled_at = now() WHERE id = @user", new { user = SeedIds.Users.Priya });
        try
        {
            using var response = await PostValidAsync(upload);

            await ExpectControlAcceptedAsync();
            await ExpectNotValidAsync(response);
            _delivery.Entered.ShouldBe(1);
        }
        finally
        {
            await RunAsync("UPDATE app_user SET disabled_at = NULL WHERE id = @user", new { user = SeedIds.Users.Priya });
        }
    }

    [Test]
    public async Task Upload_WhenMembershipLost_ShouldBeTheSameNotFoundAndNeverReachDelivery()
    {
        var upload = await NewAsync(SeedIds.Users.Priya);
        await RunAsync("UPDATE project_member SET archived_at = now() WHERE user_id = @user AND project_id = @project", new { user = SeedIds.Users.Priya, project = SeedIds.Projects.Ship });
        try
        {
            using var response = await PostValidAsync(upload);

            await ExpectControlAcceptedAsync();
            await ExpectNotValidAsync(response);
            _delivery.Entered.ShouldBe(1);
            (await RunQueryAsync<bool>("SELECT used_at IS NULL FROM transfer_token WHERE work_item_id = @itemId", new { itemId = upload.ItemId })).ShouldBeTrue();
        }
        finally
        {
            await RunAsync("UPDATE project_member SET archived_at = NULL WHERE user_id = @user AND project_id = @project", new { user = SeedIds.Users.Priya, project = SeedIds.Projects.Ship });
        }
    }

    [Test]
    public async Task Upload_WhenAuthorizationRevoked_ShouldBeTheSameNotFoundAndNeverReachDelivery()
    {
        await using var database = await OpenDatabaseAsync();
        var clientId = await new OAuthClientBuilder().InsertAsync(database);
        var authorizationId = Guid.NewGuid();
        await database.ExecuteAsync(
            "INSERT INTO oauth_authorization (id, user_id, client_id, scopes, security_stamp) SELECT @authorizationId, id, @clientId, ARRAY['shiplean:write'], security_stamp FROM app_user WHERE id = @user",
            new { authorizationId, clientId, user = SeedIds.Users.Joel });
        var live = await NewAsync(authorizationId: authorizationId);
        (await PostValidAsync(live)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var upload = await NewAsync(authorizationId: authorizationId);
        await database.ExecuteAsync("UPDATE oauth_authorization SET revoked_at = now() WHERE id = @authorizationId", new { authorizationId });

        using var response = await PostValidAsync(upload);

        await ExpectNotValidAsync(response);
        _delivery.Entered.ShouldBe(1);
    }

    [Test]
    public async Task Upload_WhenTokenUnknownOrMissing_ShouldBeNotFound()
    {
        using var unknown = await Client.PostAsync("api/uploads/demo?token=not-a-real-token", DemoUploadArrange.Form(Chapters(), "# The demo", DemoUploadArrange.Mp4()));
        using var missing = await Client.PostAsync("api/uploads/demo", DemoUploadArrange.Form(Chapters(), "# The demo", DemoUploadArrange.Mp4()));

        await ExpectNotValidAsync(unknown);
        await ExpectNotValidAsync(missing);
        _delivery.Entered.ShouldBe(0);
    }
}
