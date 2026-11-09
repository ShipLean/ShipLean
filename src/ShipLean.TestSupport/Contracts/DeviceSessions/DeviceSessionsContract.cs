using ShipLean.Application.Identity;
using ShipLean.Contracts.DeviceSessions;
using Shouldly;

namespace ShipLean.TestSupport.Contracts.DeviceSessions;

// Behaviour every IDeviceSessions has: the pinned rules of K4 (IDeviceSessions' comment), one test each. Time is never
// frozen: each test reads the clock once and passes that instant, or one before or after it, as `now`.
[TestFixture]
public abstract class DeviceSessionsContract
{
    // The RFC 7636 appendix B pair.
    protected const string Verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
    protected const string Challenge = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";

    protected abstract IDeviceSessions Sessions { get; }

    // Users the store accepts (the Postgres store needs real rows).
    protected virtual Guid UserId => SeedIds.Users.Joel;

    protected virtual Guid OtherUserId => SeedIds.Users.Priya;

    private DateTimeOffset _now;

    [SetUp]
    public void ReadTheClock() => _now = TimeProvider.System.GetUtcNow();

    private async Task<DeviceSignIn> SignInAsync(Guid? userId = null, string stamp = "stamp-1", string name = "Joel's iPhone", DateTimeOffset? at = null)
    {
        var now = at ?? _now;
        var code = await Sessions.CreateCodeAsync(userId ?? UserId, stamp, Challenge, name, now);
        return (await Sessions.ExchangeAsync(code, Verifier, now)).ShouldNotBeNull();
    }

    [Test]
    public async Task Exchange_WhenCodeAndVerifierMatch_ShouldCreateTheSession()
    {
        var signIn = await SignInAsync();

        var session = signIn.Session;
        session.UserId.ShouldBe(UserId);
        session.Name.ShouldBe("Joel's iPhone");
        session.CreatedAt.ShouldBe(_now, TimeSpan.FromMilliseconds(1));
        session.LastUsedAt.ShouldBe(_now, TimeSpan.FromMilliseconds(1));
        session.ExpiresAt.ShouldBe(_now + DeviceSessionLimits.SessionLifetime, TimeSpan.FromMilliseconds(1));
        session.RevokedAt.ShouldBeNull();
        DeviceSecret.HasShape(signIn.Secret).ShouldBeTrue();
        session.TokenPrefix.ShouldBe(signIn.Secret[..16]);
    }

    [Test]
    public async Task Exchange_ShouldCopyTheStampTheCodeWasMadeWith()
    {
        var signIn = await SignInAsync(stamp: "stamp-when-confirmed");

        signIn.Session.SecurityStamp.ShouldBe("stamp-when-confirmed");
        (await Sessions.FindBySecretAsync(signIn.Secret)).ShouldNotBeNull().SecurityStamp.ShouldBe("stamp-when-confirmed");
    }

    [Test]
    public async Task Exchange_WhenCodeUsedTwice_ShouldRefuseTheSecond()
    {
        var code = await Sessions.CreateCodeAsync(UserId, "stamp-1", Challenge, "Phone", _now);
        (await Sessions.ExchangeAsync(code, Verifier, _now)).ShouldNotBeNull();

        (await Sessions.ExchangeAsync(code, Verifier, _now)).ShouldBeNull();
    }

    [Test]
    public async Task Exchange_WhenCodeIsTwoMinutesOld_ShouldRefuse()
    {
        var code = await Sessions.CreateCodeAsync(UserId, "stamp-1", Challenge, "Phone", _now);

        (await Sessions.ExchangeAsync(code, Verifier, _now + DeviceSessionLimits.CodeLifetime)).ShouldBeNull();
    }

    [Test]
    public async Task Exchange_WhenCodeIsJustUnderTwoMinutesOld_ShouldSucceed()
    {
        var code = await Sessions.CreateCodeAsync(UserId, "stamp-1", Challenge, "Phone", _now);

        (await Sessions.ExchangeAsync(code, Verifier, _now + DeviceSessionLimits.CodeLifetime - TimeSpan.FromSeconds(1))).ShouldNotBeNull();
    }

    [Test]
    public async Task Exchange_WhenVerifierWrong_ShouldUseUpTheCode()
    {
        var code = await Sessions.CreateCodeAsync(UserId, "stamp-1", Challenge, "Phone", _now);
        (await Sessions.ExchangeAsync(code, "wrong-verifier-wrong-verifier-wrong-verifier-wrong", _now)).ShouldBeNull();

        (await Sessions.ExchangeAsync(code, Verifier, _now)).ShouldBeNull();
    }

    // S256 only: the challenge itself (what "plain" would accept) is not a verifier.
    [Test]
    public async Task Exchange_WhenVerifierIsTheChallenge_ShouldRefuse()
    {
        var code = await Sessions.CreateCodeAsync(UserId, "stamp-1", Challenge, "Phone", _now);

        (await Sessions.ExchangeAsync(code, Challenge, _now)).ShouldBeNull();
    }

    [Test]
    public async Task Exchange_WhenCodeUnknown_ShouldRefuse()
    {
        (await Sessions.ExchangeAsync(DeviceSecret.NewCode(), Verifier, _now)).ShouldBeNull();
    }

    [Test]
    public async Task Exchange_WhenConcurrent_ShouldLetExactlyOneWin()
    {
        var code = await Sessions.CreateCodeAsync(UserId, "stamp-1", Challenge, "Phone", _now);

        // Every call waits at the gate and is released together, so the exchanges really overlap.
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var exchanges = Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
        {
            await gate.Task;
            return await Sessions.ExchangeAsync(code, Verifier, _now);
        })).ToList();
        gate.SetResult();
        var results = await Task.WhenAll(exchanges);

        results.Count(r => r != null).ShouldBe(1);
    }

    [Test]
    public async Task FindBySecret_WhenRevoked_ShouldStillReturnTheSession()
    {
        var signIn = await SignInAsync();
        await Sessions.RevokeAsync(UserId, signIn.Session.Id, _now);

        (await Sessions.FindBySecretAsync(signIn.Secret)).ShouldNotBeNull().RevokedAt.ShouldNotBeNull();
    }

    [Test]
    public async Task FindBySecret_WhenExpired_ShouldStillReturnTheSession()
    {
        var signIn = await SignInAsync(at: _now - DeviceSessionLimits.SessionLifetime - TimeSpan.FromMinutes(1));

        var found = (await Sessions.FindBySecretAsync(signIn.Secret)).ShouldNotBeNull();
        found.IsLiveAt(_now).ShouldBeFalse();
    }

    [Test]
    public async Task FindBySecret_WhenUnknown_ShouldReturnNull()
    {
        (await Sessions.FindBySecretAsync(DeviceSecret.New())).ShouldBeNull();
    }

    [Test]
    public async Task Touch_WhenUnderAMinuteSinceLastUse_ShouldNotWrite()
    {
        var signIn = await SignInAsync();

        await Sessions.TouchAsync(signIn.Session.Id, _now + TimeSpan.FromSeconds(59));

        var found = (await Sessions.FindBySecretAsync(signIn.Secret)).ShouldNotBeNull();
        found.LastUsedAt.ShouldBe(signIn.Session.LastUsedAt);
        found.ExpiresAt.ShouldBe(signIn.Session.ExpiresAt);
    }

    [Test]
    public async Task Touch_WhenExactlyAMinuteSinceLastUse_ShouldSlide()
    {
        var signIn = await SignInAsync();
        var later = _now + DeviceSessionLimits.TouchInterval;

        await Sessions.TouchAsync(signIn.Session.Id, later);

        var found = (await Sessions.FindBySecretAsync(signIn.Secret)).ShouldNotBeNull();
        found.LastUsedAt.ShouldBe(later, TimeSpan.FromMilliseconds(1));
        found.ExpiresAt.ShouldBe(later + DeviceSessionLimits.SessionLifetime, TimeSpan.FromMilliseconds(1));
    }

    [Test]
    public async Task Touch_WhenAMinuteSinceLastUse_ShouldSlideNinetyDaysFromNow()
    {
        var signIn = await SignInAsync();
        var later = _now + TimeSpan.FromDays(10);

        await Sessions.TouchAsync(signIn.Session.Id, later);

        var found = (await Sessions.FindBySecretAsync(signIn.Secret)).ShouldNotBeNull();
        found.LastUsedAt.ShouldBe(later, TimeSpan.FromMilliseconds(1));
        found.ExpiresAt.ShouldBe(later + DeviceSessionLimits.SessionLifetime, TimeSpan.FromMilliseconds(1));
    }

    [Test]
    public async Task List_ShouldLeaveOutRevokedExpiredAndOtherUsers_NewestFirst()
    {
        var older = await SignInAsync(name: "Older", at: _now - TimeSpan.FromDays(2));
        var newer = await SignInAsync(name: "Newer", at: _now - TimeSpan.FromDays(1));
        var revoked = await SignInAsync(name: "Revoked");
        await Sessions.RevokeAsync(UserId, revoked.Session.Id, _now);
        await SignInAsync(name: "Expired", at: _now - DeviceSessionLimits.SessionLifetime - TimeSpan.FromMinutes(1));
        await SignInAsync(userId: OtherUserId, name: "Priya's");

        var list = await Sessions.ListAsync(UserId, _now);

        list.Select(s => s.Id).ShouldBe([newer.Session.Id, older.Session.Id]);
    }

    [Test]
    public async Task Revoke_ShouldBeTrueOnlyForTheCallThatRevoked()
    {
        var signIn = await SignInAsync();

        (await Sessions.RevokeAsync(UserId, signIn.Session.Id, _now)).ShouldBeTrue();
        (await Sessions.RevokeAsync(UserId, signIn.Session.Id, _now)).ShouldBeFalse();
        (await Sessions.FindBySecretAsync(signIn.Secret)).ShouldNotBeNull().RevokedAt.ShouldNotBeNull();
    }

    [Test]
    public async Task Revoke_WhenAnotherUsersSession_ShouldBeFalseAndLeaveItLive()
    {
        var signIn = await SignInAsync(userId: OtherUserId);

        (await Sessions.RevokeAsync(UserId, signIn.Session.Id, _now)).ShouldBeFalse();
        (await Sessions.FindBySecretAsync(signIn.Secret)).ShouldNotBeNull().RevokedAt.ShouldBeNull();
    }

    [Test]
    public async Task Revoke_WhenUnknown_ShouldBeFalse()
    {
        (await Sessions.RevokeAsync(UserId, Guid.NewGuid(), _now)).ShouldBeFalse();
    }

    [Test]
    public async Task End_ShouldBeTrueOnlyTheFirstTime()
    {
        var signIn = await SignInAsync();

        (await Sessions.EndAsync(signIn.Session.Id, _now)).ShouldBeTrue();
        (await Sessions.EndAsync(signIn.Session.Id, _now)).ShouldBeFalse();
        (await Sessions.FindBySecretAsync(signIn.Secret)).ShouldNotBeNull().IsLiveAt(_now).ShouldBeFalse();
    }
}
