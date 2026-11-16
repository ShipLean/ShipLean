using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ShipLean.Application.Database;
using ShipLean.Application.Identity;
using ShipLean.Contracts;
using ShipLean.Contracts.Profile;
using ShipLean.Contracts.Usage;
using ShipLean.Contracts.Users;
using ShipLean.Core;
using ShipLean.LocalEnvironment.Database;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Identity;
using ShipLean.TestSupport.Usage;
using ShipLean.WebApi.Tests.Auth;

namespace ShipLean.WebApi.Tests.Profile;

// The viewer's time zone: PUT /api/me/time-zone, GET /api/me/time-zones, and the zone GET /api/me reads through IViewerZone.
// One seeded scenario; each test arranges users of its own. Seeded users have no zone, so they read in UTC (MeTests pins Joel).
[TestFixture]
public class TimeZoneApiTests : ApiFixture
{
    private const string NotSet = "Not set: times show in UTC";
    private const string London = "Europe/London";
    private const string Password = "Time-zone-pass-1";

    private static int _next;

    // The cookie's sliding renewal is stepped over with this clock. It starts at the real time: the client's cookie jar
    // checks the cookie's expiry against the real one.
    private readonly FakeTimeProvider _clock = new(TimeProvider.System.GetUtcNow());

    protected override Action<IServiceCollection>? ConfigureTestServices => services => services.AddSingleton<TimeProvider>(_clock);

    private RecordingUsageEvents Usage => (RecordingUsageEvents)Host.Services.GetRequiredService<IUsageEvents>();

    [SetUp]
    public void ClearUsage() => Usage.Clear();

    private async Task<TestUser> ArrangeUserAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        var number = Interlocked.Increment(ref _next);
        var email = $"zone-{number}@example.test";
        var id = await new AppUserBuilder().WithEmail(email).WithName($"Zone Person {number}").WithPassword(Password).InsertAsync(connection);
        return new TestUser(id, email, Password);
    }

    private async Task StoreZoneAsync(TestUser user, string zone)
    {
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync("UPDATE app_user SET time_zone = @zone WHERE id = @id", new { zone, id = user.Id });
    }

    private Task<HttpResponseMessage> PutZoneAsync(TestUser user, string zone, string source)
        => Api.ActingAs(user).PutJsonAsync("/api/me/time-zone", new { timeZone = zone, source });

    private async Task PutZoneOkAsync(TestUser user, string zone, string source)
    {
        using var response = await PutZoneAsync(user, zone, source);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static string LabelOf(string zone) =>
        DisplayDate.ZoneLabel(DisplayDate.TryZone(zone, out var found) ? found : throw new ArgumentException(zone, nameof(zone)), TimeProvider.System.GetUtcNow());

    // Mutation: drop `time_zone = @zone` from the UPDATE.
    [Test]
    public async Task SetTimeZone_WhenProfileSetsAsiaBangkok_ShouldAnswerMeWithZoneAndLabel()
    {
        var user = await ArrangeUserAsync();

        using var response = await PutZoneAsync(user, ViewerZoneTestData.BangkokId, "profile");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var answered = await ProfileWire.ReadMeAsync(response);
        answered.TimeZone.ShouldBe("Asia/Bangkok");
        answered.TimeZoneLabel.ShouldBe("Asia/Bangkok (UTC+07:00)");
        var me = await ProfileWire.GetMeAsync(Api, user);
        me.TimeZone.ShouldBe("Asia/Bangkok");
        me.TimeZoneLabel.ShouldBe("Asia/Bangkok (UTC+07:00)");
    }

    [Test]
    public async Task SetTimeZone_WhenBrowserSourceAndNoZone_ShouldStoreTheBrowsersZone()
    {
        var user = await ArrangeUserAsync();

        using var response = await PutZoneAsync(user, London, "browser");

        (await ProfileWire.ReadMeAsync(response)).TimeZone.ShouldBe(London);
        (await ProfileWire.GetMeAsync(Api, user)).TimeZoneLabel.ShouldBe(LabelOf(London));
    }

    // Mutation: remove the `time_zone IS NULL` guard (`@fromProfile OR time_zone IS NULL` -> `true`), so a browser value overwrites a choice.
    [Test]
    public async Task SetTimeZone_WhenBrowserSourceAndZoneAlreadySet_ShouldKeepTheStoredZone()
    {
        var user = await ArrangeUserAsync();
        await ViewerZoneArrange.SetAsync(Api, user, London);

        using var response = await PutZoneAsync(user, ViewerZoneTestData.BangkokId, "browser");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ProfileWire.ReadMeAsync(response)).TimeZone.ShouldBe(London);
        (await ProfileWire.GetMeAsync(Api, user)).TimeZone.ShouldBe(London);
    }

    // Mutation: drop the HasIanaId check in DisplayDate.TryZone, so the Windows id passes.
    [TestCase("Bangkok")]
    [TestCase("SE Asia Standard Time")]
    [TestCase("+07:00")]
    [TestCase("")]
    [TestCase("Asia/Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] // 65 characters
    public async Task SetTimeZone_WhenIdIsNotIana_ShouldAnswerValidation(string zone)
    {
        var user = await ArrangeUserAsync();

        using var response = await PutZoneAsync(user, zone, "profile");

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe("validation");
        error.Error.ShouldBe("The time zone must be an IANA name such as Asia/Bangkok.");
        (await ProfileWire.GetMeAsync(Api, user)).TimeZone.ShouldBeNull();
    }

    // The user's zone is cached for a minute (UserSessions); the change must reach the next request at once.
    // Mutation: remove `sessions.Forget(userId)` from SetTimeZoneAsync.
    [Test]
    public async Task SetTimeZone_WhenZoneChanges_ShouldShowInTheNextRequestsLabel()
    {
        var user = await ArrangeUserAsync();
        await StoreZoneAsync(user, London);
        (await ProfileWire.GetMeAsync(Api, user)).TimeZone.ShouldBe(London);

        using var response = await PutZoneAsync(user, ViewerZoneTestData.BangkokId, "profile");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var me = await ProfileWire.GetMeAsync(Api, user);
        me.TimeZone.ShouldBe("Asia/Bangkok");
        me.TimeZoneLabel.ShouldBe("Asia/Bangkok (UTC+07:00)");
    }

    // Mutation: order ZoneChoices by id only (the offset order is lost). On Linux every system zone has an IANA id, so
    // dropping the HasIanaId filter cannot be shown red here.
    [Test]
    public async Task TimeZoneChoices_WhenRead_ShouldListIanaZonesWithOffsetLabels()
    {
        var choices = await Api.GetJsonAsync<TimeZoneChoicesDto>("/api/me/time-zones");

        choices.Choices.ShouldContain(new TimeZoneChoiceDto("Asia/Bangkok", "Asia/Bangkok (UTC+07:00)"));
        choices.Choices.ShouldContain(new TimeZoneChoiceDto(London, LabelOf(London)));
        choices.Choices.Where(choice => !DisplayDate.TryZone(choice.Id, out _) || choice.Id.Contains(' ', StringComparison.Ordinal)).ShouldBeEmpty();
        var now = TimeProvider.System.GetUtcNow();
        var offsets = choices.Choices.Select(choice => DisplayDate.TryZone(choice.Id, out var zone) ? zone.GetUtcOffset(now) : TimeSpan.MaxValue).ToList();
        offsets.ShouldBe(offsets.Order().ToList());
    }

    // The database only checks a stored id's shape; an id this host does not know reads as not set.
    // Mutation: ViewerZone returns the raw stored id when DisplayDate.TryZone fails.
    [Test]
    public async Task ViewerZone_WhenStoredIdIsUnknown_ShouldFallBackToUtcAsNotSet()
    {
        var user = await ArrangeUserAsync();
        await StoreZoneAsync(user, "Mars/Olympus");

        var me = await ProfileWire.GetMeAsync(Api, user);

        me.TimeZone.ShouldBeNull();
        me.TimeZoneLabel.ShouldBe(NotSet);
    }

    // Id and Zone are both read per label, so the claim is looked up and the id checked once per request and principal.
    // Mutation: resolve on every read (drop the answer kept in HttpContext.Items).
    [Test]
    public void ViewerZone_WhenIdAndZoneAreReadOnTheSameRequest_ShouldResolveOnce()
    {
        var principal = new CountingPrincipal(new ClaimsIdentity([new Claim(ViewerZoneClaims.ZoneClaim, London)], "Test"));
        var viewerZone = new ViewerZone(new ContextProvider(principal));

        var id = viewerZone.Id;
        var zone = viewerZone.Zone;
        var again = viewerZone.Zone;

        id.ShouldBe(London);
        zone.Id.ShouldBe(London);
        again.ShouldBeSameAs(zone);
        principal.ZoneLookups.ShouldBe(1);
    }

    // A policy scheme forwards, so one request transforms twice. Mutations: mutate the incoming principal instead of a
    // clone; skip removing the earlier zone identity.
    [Test]
    public async Task ViewerZone_WhenSameRequestAuthenticatesTwice_ShouldCarryOneZoneClaim()
    {
        var user = await ArrangeUserAsync();
        await StoreZoneAsync(user, ViewerZoneTestData.BangkokId);
        var transformation = Host.Services.GetRequiredService<IClaimsTransformation>();
        var incoming = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id!.Value.ToString())], "Test"));

        var once = await transformation.TransformAsync(incoming);
        var twice = await transformation.TransformAsync(once);

        twice.FindAll(ViewerZoneClaims.ZoneClaim).Select(claim => claim.Value).ShouldBe(["Asia/Bangkok"]);
        incoming.FindAll(ViewerZoneClaims.ZoneClaim).ShouldBeEmpty();
        incoming.Identities.Count().ShouldBe(1);
        once.FindAll(ViewerZoneClaims.ZoneClaim).Count().ShouldBe(1);
        twice.FindFirstValue(ClaimTypes.NameIdentifier).ShouldBe(user.Id.Value.ToString());
    }

    // Through the real sign-in cookie, not the test scheme: each request's principal comes from the cookie, and the zone
    // follows the stored setting from one request to the next.
    [Test]
    public async Task ViewerZone_WhenCookieUserChangesZone_ShouldReadTheNewZoneOnTheNextRequest()
    {
        var user = await ArrangeUserAsync();
        using var browser = await AuthClient.SignedInAsync(Host, user.Email!, Password);
        (await ReadCookieMeAsync(browser)).TimeZone.ShouldBeNull();

        using (var first = await browser.PutAsJsonAsync("/api/me/time-zone", new { timeZone = London, source = "profile" }))
        {
            first.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
        (await ReadCookieMeAsync(browser)).TimeZone.ShouldBe(London);

        using (var second = await browser.PutAsJsonAsync("/api/me/time-zone", new { timeZone = ViewerZoneTestData.BangkokId, source = "profile" }))
        {
            second.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
        var me = await ReadCookieMeAsync(browser);
        me.TimeZone.ShouldBe("Asia/Bangkok");
        me.TimeZoneLabel.ShouldBe("Asia/Bangkok (UTC+07:00)");
    }

    // Sliding expiration re-issues the cookie from the ticket's own principal once half the lifetime has passed. The zone
    // is never written into it, so a zone changed before a renewal is read fresh and the one after reads fresh again.
    // Mutations: skip refreshing the zone claim when the principal already carries one; mutate the incoming principal.
    [Test]
    public async Task ViewerZone_WhenCookieIsRenewedBetweenRequests_ShouldReadTheChangedZoneEachTime()
    {
        var user = await ArrangeUserAsync();
        using var browser = await AuthClient.SignedInAsync(Host, user.Email!, Password);
        using (var first = await browser.PutAsJsonAsync("/api/me/time-zone", new { timeZone = London, source = "profile" }))
        {
            first.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        _clock.Advance(TimeSpan.FromDays(8));
        var (afterFirstRenewal, firstRenewed) = await ReadRenewingMeAsync(browser);
        firstRenewed.ShouldBeTrue();
        afterFirstRenewal.TimeZone.ShouldBe(London);

        using (var second = await browser.PutAsJsonAsync("/api/me/time-zone", new { timeZone = ViewerZoneTestData.BangkokId, source = "profile" }))
        {
            second.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
        _clock.Advance(TimeSpan.FromDays(8));
        var (afterSecondRenewal, secondRenewed) = await ReadRenewingMeAsync(browser);

        secondRenewed.ShouldBeTrue();
        afterSecondRenewal.TimeZone.ShouldBe("Asia/Bangkok");
        afterSecondRenewal.TimeZoneLabel.ShouldBe("Asia/Bangkok (UTC+07:00)");
    }

    // Literal wire names, not the constants. Mutations: swap the browser/profile values; emit the key as "from".
    [Test]
    public async Task SetTimeZone_WhenValueChanges_ShouldTrackTimeZoneSetWithSource()
    {
        var user = await ArrangeUserAsync();

        await PutZoneOkAsync(user, London, "browser");
        await PutZoneOkAsync(user, ViewerZoneTestData.BangkokId, "profile");

        var tracked = Usage.Events.Where(e => e.Name == "profile.time_zone_set").ToList();
        tracked.Select(e => e.Properties["zone_source"]).ShouldBe(["browser", "profile"]);
        tracked.ShouldAllBe(e => e.Source == "api:Profile.SetTimeZone");
    }

    // Mutation: track on every PUT (drop the `if (changed)`).
    [Test]
    public async Task SetTimeZone_WhenUnchanged_ShouldTrackNothing()
    {
        var user = await ArrangeUserAsync();
        await StoreZoneAsync(user, London);

        await PutZoneOkAsync(user, London, "profile");
        await PutZoneOkAsync(user, ViewerZoneTestData.BangkokId, "browser");

        Usage.Events.Where(e => e.Name == "profile.time_zone_set").ShouldBeEmpty();
    }

    // The migration backfills every user who existed before it with Asia/Bangkok. The seeded template applies migrations
    // before the seed, so this rebuilds the state before the migration on an empty clone: drop the column (only this
    // migration touches it), insert a user, run the migration's script. Mutation: drop the UPDATE from the migration.
    [Test]
    public async Task UsersTimeZoneMigration_WhenAppliedToExistingUsers_ShouldBackfillAsiaBangkok()
    {
        var script = DatabaseScripts.Load(includeSeed: false).Single(s => s.Name.EndsWith("202611161000_users_time_zone.sql", StringComparison.Ordinal));
        await using var database = await ShipLeanDatabase.CloneEmptyAsync();
        await using var connection = await database.OpenAsync();
        await connection.ExecuteAsync("ALTER TABLE app_user DROP COLUMN time_zone");
        var id = await new AppUserBuilder().WithEmail("before-zones@example.test").InsertAsync(connection);

        await connection.ExecuteAsync(script.Contents);

        (await connection.ExecuteScalarAsync<string>("SELECT time_zone FROM app_user WHERE id = @id", new { id })).ShouldBe("Asia/Bangkok");
    }

    private static async Task<(MeDto Me, bool Renewed)> ReadRenewingMeAsync(HttpClient browser)
    {
        using var response = await browser.GetAsync("/api/me");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var renewed = response.Headers.TryGetValues("Set-Cookie", out var cookies) && cookies.Any();
        return (await ProfileWire.ReadMeAsync(response), renewed);
    }

    private static async Task<MeDto> ReadCookieMeAsync(HttpClient browser)
    {
        using var response = await browser.GetAsync("/api/me");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await ProfileWire.ReadMeAsync(response);
    }

    private sealed class ContextProvider(ClaimsPrincipal user) : IServiceProvider
    {
        private readonly HttpContextAccessor _accessor = new() { HttpContext = new DefaultHttpContext { User = user } };

        public object? GetService(Type serviceType) => serviceType == typeof(IHttpContextAccessor) ? _accessor : null;
    }

    private sealed class CountingPrincipal(ClaimsIdentity identity) : ClaimsPrincipal(identity)
    {
        public int ZoneLookups { get; private set; }

        public override Claim? FindFirst(string type)
        {
            if (type == ViewerZoneClaims.ZoneClaim)
            {
                ZoneLookups++;
            }
            return base.FindFirst(type);
        }
    }
}
