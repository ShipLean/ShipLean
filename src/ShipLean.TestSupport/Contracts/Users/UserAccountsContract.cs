using Shouldly;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Users;
using ShipLean.TestSupport.Builders;

namespace ShipLean.TestSupport.Contracts.Users;

// Behaviour every IUserAccounts has. The accounts act as whoever ActAs last named. The store starts with no users at all
// (a fresh database or an empty fake), so the instance-admin rules depend only on what a test arranges.
[TestFixture]
public abstract class UserAccountsContract
{
    private const string NewPassword = "a-brand-new-password";

    protected abstract IUserAccounts Accounts { get; }

    protected abstract IUserDirectory Directory { get; }

    protected abstract Task<ArrangedUser> ArrangeUserAsync(UserSpec spec);

    protected abstract void ActAs(Guid userId);

    protected abstract Task<DateTimeOffset?> LastSignInAtAsync(Guid id);

    [Test]
    public async Task Accounts_WhenOneTimePasswordIssued_ShouldSetMustChange()
    {
        var user = await ArrangeUserAsync(new UserSpec { Status = UserStatus.NoPassword });
        var before = await Accounts.GetSecurityStampAsync(user.Id);

        var issued = await Accounts.IssueOneTimePasswordAsync(user.Id);

        issued.UserId.ShouldBe(user.Id);
        issued.Password.Length.ShouldBeGreaterThanOrEqualTo(12);
        (await Directory.GetAsync(user.Id)).Status.ShouldBe(UserStatus.OneTimePassword);
        (await Accounts.GetSecurityStampAsync(user.Id)).ShouldNotBe(before);
        ActAs(user.Id);
        await Accounts.ChangePasswordAsync(issued.Password, NewPassword);
        (await Directory.GetAsync(user.Id)).Status.ShouldBe(UserStatus.Active);
    }

    [Test]
    public async Task Accounts_WhenOneTimePasswordIssuedTwice_ShouldGiveDifferentPasswords()
    {
        var user = await ArrangeUserAsync(new UserSpec());

        var first = await Accounts.IssueOneTimePasswordAsync(user.Id);
        var second = await Accounts.IssueOneTimePasswordAsync(user.Id);

        second.Password.ShouldNotBe(first.Password);
    }

    [Test]
    public async Task Accounts_WhenOneTimePasswordIssuedToALockedUser_ShouldClearTheLockout()
    {
        var user = await ArrangeUserAsync(new UserSpec { Status = UserStatus.Locked });

        await Accounts.IssueOneTimePasswordAsync(user.Id);

        (await Directory.GetAsync(user.Id)).Status.ShouldBe(UserStatus.OneTimePassword);
    }

    [Test]
    public async Task Accounts_WhenUnlocked_ShouldBeActive()
    {
        var user = await ArrangeUserAsync(new UserSpec { Status = UserStatus.Locked });

        await Accounts.UnlockAsync(user.Id);

        (await Directory.GetAsync(user.Id)).Status.ShouldBe(UserStatus.Active);
    }

    [Test]
    public async Task Accounts_WhenUserIsUnknown_ShouldBeNotFound()
    {
        var unknown = Guid.NewGuid();

        (await Should.ThrowAsync<ApiException>(() => Accounts.IssueOneTimePasswordAsync(unknown))).Code.ShouldBe(ApiErrorCodes.NotFound);
        (await Should.ThrowAsync<ApiException>(() => Accounts.UnlockAsync(unknown))).Code.ShouldBe(ApiErrorCodes.NotFound);
        (await Should.ThrowAsync<ApiException>(() => Accounts.SetDisabledAsync(unknown, true))).Code.ShouldBe(ApiErrorCodes.NotFound);
        (await Should.ThrowAsync<ApiException>(() => Accounts.SetInstanceAdminAsync(unknown, true))).Code.ShouldBe(ApiErrorCodes.NotFound);
        (await Should.ThrowAsync<ApiException>(() => Accounts.ChangeEmailAsync(unknown, "x@example.test"))).Code.ShouldBe(ApiErrorCodes.NotFound);
        (await Should.ThrowAsync<ApiException>(() => Accounts.GetSecurityStampAsync(unknown))).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Accounts_WhenDisabledAndEnabled_ShouldChangeStatusAndRotateTheStampOnDisable()
    {
        var caller = await ArrangeUserAsync(new UserSpec { IsInstanceAdmin = true });
        var user = await ArrangeUserAsync(new UserSpec());
        ActAs(caller.Id);
        var before = await Accounts.GetSecurityStampAsync(user.Id);

        await Accounts.SetDisabledAsync(user.Id, true);

        (await Directory.GetAsync(user.Id)).Status.ShouldBe(UserStatus.Disabled);
        (await Accounts.GetSecurityStampAsync(user.Id)).ShouldNotBe(before);

        await Accounts.SetDisabledAsync(user.Id, false);

        (await Directory.GetAsync(user.Id)).Status.ShouldBe(UserStatus.Active);
    }

    [Test]
    public async Task Accounts_WhenLastActiveAdminDisabled_ShouldBeValidation()
    {
        var onlyAdmin = await ArrangeUserAsync(new UserSpec { IsInstanceAdmin = true });
        await ArrangeUserAsync(new UserSpec { IsInstanceAdmin = true, Status = UserStatus.Disabled });
        var caller = await ArrangeUserAsync(new UserSpec());
        ActAs(caller.Id);

        var error = await Should.ThrowAsync<ApiException>(() => Accounts.SetDisabledAsync(onlyAdmin.Id, true));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
        (await Directory.GetAsync(onlyAdmin.Id)).Status.ShouldBe(UserStatus.Active);
    }

    [Test]
    public async Task Accounts_WhenAnotherActiveAdminRemains_ShouldDisableAnAdmin()
    {
        var first = await ArrangeUserAsync(new UserSpec { IsInstanceAdmin = true });
        var second = await ArrangeUserAsync(new UserSpec { IsInstanceAdmin = true });
        ActAs(second.Id);

        await Accounts.SetDisabledAsync(first.Id, true);

        (await Directory.GetAsync(first.Id)).Status.ShouldBe(UserStatus.Disabled);
    }

    [Test]
    public async Task Accounts_WhenDisablingSelf_ShouldBeValidation()
    {
        var self = await ArrangeUserAsync(new UserSpec { IsInstanceAdmin = true });
        await ArrangeUserAsync(new UserSpec { IsInstanceAdmin = true });
        ActAs(self.Id);

        var error = await Should.ThrowAsync<ApiException>(() => Accounts.SetDisabledAsync(self.Id, true));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Accounts_WhenDemotingSelf_ShouldBeValidation()
    {
        var self = await ArrangeUserAsync(new UserSpec { IsInstanceAdmin = true });
        await ArrangeUserAsync(new UserSpec { IsInstanceAdmin = true });
        ActAs(self.Id);

        var error = await Should.ThrowAsync<ApiException>(() => Accounts.SetInstanceAdminAsync(self.Id, false));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Accounts_WhenLastActiveAdminDemoted_ShouldBeValidation()
    {
        var onlyAdmin = await ArrangeUserAsync(new UserSpec { IsInstanceAdmin = true });
        var caller = await ArrangeUserAsync(new UserSpec());
        ActAs(caller.Id);

        var error = await Should.ThrowAsync<ApiException>(() => Accounts.SetInstanceAdminAsync(onlyAdmin.Id, false));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
        (await Directory.GetAsync(onlyAdmin.Id)).IsInstanceAdmin.ShouldBeTrue();
    }

    [Test]
    public async Task Accounts_WhenPromotedAndDemoted_ShouldChangeIsInstanceAdmin()
    {
        var admin = await ArrangeUserAsync(new UserSpec { IsInstanceAdmin = true });
        var user = await ArrangeUserAsync(new UserSpec());
        ActAs(admin.Id);

        await Accounts.SetInstanceAdminAsync(user.Id, true);
        (await Directory.GetAsync(user.Id)).IsInstanceAdmin.ShouldBeTrue();

        await Accounts.SetInstanceAdminAsync(user.Id, false);
        (await Directory.GetAsync(user.Id)).IsInstanceAdmin.ShouldBeFalse();
    }

    [Test]
    public async Task Accounts_WhenPasswordChangedWithWrongCurrent_ShouldBeValidation()
    {
        var user = await ArrangeUserAsync(new UserSpec());
        ActAs(user.Id);
        var before = await Accounts.GetSecurityStampAsync(user.Id);

        var error = await Should.ThrowAsync<ApiException>(() => Accounts.ChangePasswordAsync("not-the-password", NewPassword));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
        (await Accounts.GetSecurityStampAsync(user.Id)).ShouldBe(before);
    }

    [Test]
    public async Task Accounts_WhenNewPasswordIsTooShort_ShouldBeValidation()
    {
        var user = await ArrangeUserAsync(new UserSpec());
        ActAs(user.Id);

        var error = await Should.ThrowAsync<ApiException>(() => Accounts.ChangePasswordAsync(AppUserBuilder.ArrangedPassword, "short"));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Accounts_WhenPasswordChanged_ShouldRotateStamp()
    {
        var user = await ArrangeUserAsync(new UserSpec());
        ActAs(user.Id);
        var before = await Accounts.GetSecurityStampAsync(user.Id);

        var stamp = await Accounts.ChangePasswordAsync(AppUserBuilder.ArrangedPassword, NewPassword);

        stamp.ShouldNotBe(before);
        (await Accounts.GetSecurityStampAsync(user.Id)).ShouldBe(stamp);
        // The new password is now the current one.
        await Accounts.ChangePasswordAsync(NewPassword, "another-new-password");
    }

    [Test]
    public async Task Accounts_WhenPasswordChangedWhileAChangeIsRequired_ShouldClearTheRequirement()
    {
        var user = await ArrangeUserAsync(new UserSpec { Status = UserStatus.OneTimePassword });
        ActAs(user.Id);

        await Accounts.ChangePasswordAsync(AppUserBuilder.ArrangedPassword, NewPassword);

        (await Directory.GetAsync(user.Id)).Status.ShouldBe(UserStatus.Active);
    }

    [Test]
    public async Task Accounts_WhenEmailChanged_ShouldRotateStamp()
    {
        var user = await ArrangeUserAsync(new UserSpec());
        var before = await Accounts.GetSecurityStampAsync(user.Id);

        await Accounts.ChangeEmailAsync(user.Id, "  Changed.Address@Example.test ");

        (await Accounts.GetSecurityStampAsync(user.Id)).ShouldNotBe(before);
        (await Directory.FindByEmailAsync("changed.address@example.test"))!.Id.ShouldBe(user.Id);
        (await Directory.FindByEmailAsync(user.Email)).ShouldBeNull();
    }

    [Test]
    public async Task Accounts_WhenEmailChangedToATakenOne_ShouldBeConflict()
    {
        var user = await ArrangeUserAsync(new UserSpec());
        var other = await ArrangeUserAsync(new UserSpec());

        var error = await Should.ThrowAsync<ApiException>(() => Accounts.ChangeEmailAsync(user.Id, other.Email.ToUpperInvariant()));

        error.Code.ShouldBe(ApiErrorCodes.Conflict);
    }

    [Test]
    public async Task Accounts_WhenEmailChangedToABadOne_ShouldBeValidation()
    {
        var user = await ArrangeUserAsync(new UserSpec());

        var error = await Should.ThrowAsync<ApiException>(() => Accounts.ChangeEmailAsync(user.Id, "not an email"));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Accounts_WhenSignOutEverywhereElse_ShouldRotateStamp()
    {
        var user = await ArrangeUserAsync(new UserSpec());
        var other = await ArrangeUserAsync(new UserSpec());
        ActAs(user.Id);
        var before = await Accounts.GetSecurityStampAsync(user.Id);
        var otherBefore = await Accounts.GetSecurityStampAsync(other.Id);

        var stamp = await Accounts.SignOutEverywhereElseAsync();

        stamp.ShouldNotBe(before);
        (await Accounts.GetSecurityStampAsync(user.Id)).ShouldBe(stamp);
        (await Accounts.GetSecurityStampAsync(other.Id)).ShouldBe(otherBefore);
    }

    [Test]
    public async Task RecordSignIn_WhenCalled_ShouldSetLastSignInAt()
    {
        var user = await ArrangeUserAsync(new UserSpec());
        var stamp = await Accounts.GetSecurityStampAsync(user.Id);
        (await LastSignInAtAsync(user.Id)).ShouldBeNull();
        var before = TimeProvider.System.GetUtcNow().AddSeconds(-5);

        await Accounts.RecordSignInAsync(user.Id);

        (await LastSignInAtAsync(user.Id)).ShouldNotBeNull().ShouldBeGreaterThan(before);
        (await Accounts.GetSecurityStampAsync(user.Id)).ShouldBe(stamp);
    }

    [Test]
    public async Task RecordSignIn_WhenUserLocked_ShouldKeepTheLockout()
    {
        var user = await ArrangeUserAsync(new UserSpec { Status = UserStatus.Locked });

        await Accounts.RecordSignInAsync(user.Id);

        (await Directory.GetAsync(user.Id)).Status.ShouldBe(UserStatus.Locked);
    }

    [Test]
    public async Task RecordSignIn_WhenUserUnknown_ShouldThrowNotFound()
    {
        var error = await Should.ThrowAsync<ApiException>(() => Accounts.RecordSignInAsync(Guid.NewGuid()));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }
}
