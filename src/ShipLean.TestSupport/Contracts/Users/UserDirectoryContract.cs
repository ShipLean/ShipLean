using Shouldly;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Users;

namespace ShipLean.TestSupport.Contracts.Users;

// Behaviour every IUserDirectory has. A derived fixture builds the directory (and its store) in [SetUp] and arranges users
// and projects through the hooks; no test depends on how the implementation stores anything.
[TestFixture]
public abstract class UserDirectoryContract
{
    protected abstract IUserDirectory Directory { get; }

    protected abstract Task<ArrangedUser> ArrangeUserAsync(UserSpec spec);

    protected abstract Task<Guid> ArrangeProjectAsync();

    protected abstract Task<int> CountUsersWithEmailAsync(string email);

    protected abstract Task ArchiveMembershipAsync(Guid projectId, Guid userId);

    [Test]
    public async Task Directory_WhenPartialEmail_ShouldFindNothing()
    {
        var user = await ArrangeUserAsync(new UserSpec());
        var local = user.Email[..user.Email.IndexOf('@')];

        (await Directory.FindByEmailAsync(local)).ShouldBeNull();
        (await Directory.FindByEmailAsync(user.Email[..^3])).ShouldBeNull();
    }

    [Test]
    public async Task Directory_WhenEmailDiffersInCase_ShouldFind()
    {
        var user = await ArrangeUserAsync(new UserSpec());

        var found = await Directory.FindByEmailAsync($"  {user.Email.ToUpperInvariant()} ");

        found.ShouldNotBeNull();
        found.Id.ShouldBe(user.Id);
        found.Email.ShouldBe(user.Email);
    }

    [Test]
    public async Task Directory_WhenCreated_ShouldHaveNoPassword()
    {
        var created = await Directory.CreateAsync("new.person@example.test", "New Person");

        created.Status.ShouldBe(UserStatus.NoPassword);
        created.Name.ShouldBe("New Person");
        created.Initials.ShouldBe("NP");
        created.AvatarColour.ShouldMatch("^#[0-9a-f]{6}$");
        created.PhotoUrl.ShouldBeNull();
        created.IsInstanceAdmin.ShouldBeFalse();
        (await Directory.GetAsync(created.Id)).ShouldBe(created);
        (await Directory.FindByEmailAsync("NEW.PERSON@example.test"))!.Id.ShouldBe(created.Id);
    }

    [Test]
    public async Task Directory_WhenEmailTaken_ShouldBeConflict()
    {
        var user = await ArrangeUserAsync(new UserSpec());

        var error = await Should.ThrowAsync<ApiException>(() => Directory.CreateAsync(user.Email.ToUpperInvariant(), "Someone Else"));

        error.Code.ShouldBe(ApiErrorCodes.Conflict);
    }

    [TestCase("", "Some One")]
    [TestCase("no-at-sign", "Some One")]
    [TestCase("a@b@c.test", "Some One")]
    [TestCase("has space@example.test", "Some One")]
    [TestCase("fine@example.test", "")]
    [TestCase("fine@example.test", "   ")]
    public async Task Directory_WhenEmailOrNameIsBad_ShouldBeValidation(string email, string name)
    {
        var error = await Should.ThrowAsync<ApiException>(() => Directory.CreateAsync(email, name));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Directory_WhenIdIsUnknown_ShouldBeNotFound()
    {
        var error = await Should.ThrowAsync<ApiException>(() => Directory.GetAsync(Guid.NewGuid()));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Directory_WhenSearchingMembers_ShouldReturnLiveMembersWhoseNameOrEmailStartsWithTheTextInNameOrder()
    {
        var project = await ArrangeProjectAsync();
        var otherProject = await ArrangeProjectAsync();
        var zed = await ArrangeUserAsync(new UserSpec { Name = "Zed Zimmer", ProjectIds = [project] });
        var amy = await ArrangeUserAsync(new UserSpec { Name = "Amy Adams", ProjectIds = [project] });
        var amir = await ArrangeUserAsync(new UserSpec { Name = "amir Khan", ProjectIds = [project] });
        var left = await ArrangeUserAsync(new UserSpec { Name = "Amanda Left", ProjectIds = [project] });
        await ArchiveMembershipAsync(project, left.Id);
        await ArrangeUserAsync(new UserSpec { Name = "Amos Elsewhere", ProjectIds = [otherProject] });
        await ArrangeUserAsync(new UserSpec { Name = "Amelia Gone", Status = UserStatus.Disabled, ProjectIds = [project] });

        var byName = await Directory.SearchMembersAsync(project, "AM");
        var byEmail = await Directory.SearchMembersAsync(project, zed.Email[..5]);
        var everyone = await Directory.SearchMembersAsync(project, string.Empty);

        byName.Select(u => u.Id).ShouldBe([amir.Id, amy.Id]);
        byName[0].Name.ShouldBe("amir Khan");
        byName[0].Initials.ShouldBe("AK");
        byEmail.Select(u => u.Id).ShouldBe([zed.Id]);
        everyone.Select(u => u.Id).ShouldBe([amir.Id, amy.Id, zed.Id]);
        (await Directory.SearchMembersAsync(project, "Qq")).ShouldBeEmpty();
    }

    [Test]
    public async Task Directory_WhenSearchingMembers_ShouldSortOrdinallyIgnoringCase()
    {
        var project = await ArrangeProjectAsync();
        var joel = await ArrangeUserAsync(new UserSpec { Name = "Joel Zed", ProjectIds = [project] });
        var jo = await ArrangeUserAsync(new UserSpec { Name = "Jo Smith", ProjectIds = [project] });
        var joAnn = await ArrangeUserAsync(new UserSpec { Name = "Jo-Ann Lee", ProjectIds = [project] });
        var joanna = await ArrangeUserAsync(new UserSpec { Name = "joanna Bee", ProjectIds = [project] });

        var found = await Directory.SearchMembersAsync(project, "jo");

        found.Select(u => u.Id).ShouldBe([jo.Id, joAnn.Id, joanna.Id, joel.Id]);
    }

    [Test]
    public async Task FindOrCreate_WhenNoUserHasTheEmail_ShouldCreateWithTheName()
    {
        var user = await Directory.FindOrCreateAsync("  Fresh.Face@example.test ", "Fresh Face");

        user.Name.ShouldBe("Fresh Face");
        user.Initials.ShouldBe("FF");
        user.Email.ShouldBe("Fresh.Face@example.test");
        user.Status.ShouldBe(UserStatus.NoPassword);
        (await Directory.GetAsync(user.Id)).ShouldBe(user);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public async Task FindOrCreate_WhenNameMissing_ShouldNameFromEmailPrefix(string? name)
    {
        var user = await Directory.FindOrCreateAsync("prefix.person@example.test", name);

        user.Name.ShouldBe("prefix.person");
    }

    private static readonly string TooLong = new('x', 101);

    [TestCase("bad\u0007name")]
    [TestCase("zero\u200Bwidth")]
    public async Task FindOrCreate_WhenNameRejectedOnCreate_ShouldNameFromEmailPrefix(string name)
    {
        var user = await Directory.FindOrCreateAsync("rejected.name@example.test", name);

        user.Name.ShouldBe("rejected.name");
    }

    [Test]
    public async Task FindOrCreate_WhenNameTooLongOnCreate_ShouldNameFromEmailPrefix()
    {
        var user = await Directory.FindOrCreateAsync("too.long@example.test", TooLong);

        user.Name.ShouldBe("too.long");
    }

    [TestCase("bad\u0007name")]
    [TestCase("zero\u200Bwidth")]
    [TestCase("")]
    public async Task FindOrCreate_WhenNameRejectedOnFind_ShouldKeepTheStoredName(string name)
    {
        var existing = await ArrangeUserAsync(new UserSpec());

        var found = await Directory.FindOrCreateAsync(existing.Email, name);

        found.Name.ShouldBe(existing.Name);
    }

    [Test]
    public async Task FindOrCreate_WhenNameTooLongOnFind_ShouldKeepTheStoredName()
    {
        var existing = await ArrangeUserAsync(new UserSpec());

        var found = await Directory.FindOrCreateAsync(existing.Email, TooLong);

        found.Name.ShouldBe(existing.Name);
    }

    [Test]
    public async Task FindOrCreate_WhenEmailPrefixIsUnusable_ShouldStillCreateAValidName()
    {
        var user = await Directory.FindOrCreateAsync(new string('y', 150) + "@example.test", null);

        user.Name.ShouldBe(new string('y', 100));
    }

    [Test]
    public async Task FindOrCreate_WhenEmailDiffersInCase_ShouldReturnTheExistingUser()
    {
        var existing = await ArrangeUserAsync(new UserSpec());

        var found = await Directory.FindOrCreateAsync(existing.Email.ToUpperInvariant(), null);

        found.Id.ShouldBe(existing.Id);
        (await CountUsersWithEmailAsync(existing.Email)).ShouldBe(1);
    }

    [Test]
    public async Task FindOrCreate_WhenNameDiffers_ShouldUpdateNameAndInitials()
    {
        var existing = await ArrangeUserAsync(new UserSpec());

        var found = await Directory.FindOrCreateAsync(existing.Email, "Renamed Person");

        found.Id.ShouldBe(existing.Id);
        found.Name.ShouldBe("Renamed Person");
        found.Initials.ShouldBe("RP");
        (await Directory.GetAsync(existing.Id)).ShouldBe(found);
    }

    [Test]
    public async Task FindOrCreate_WhenNameNull_ShouldKeepTheStoredName()
    {
        var existing = await ArrangeUserAsync(new UserSpec());

        var found = await Directory.FindOrCreateAsync(existing.Email, null);

        found.Name.ShouldBe(existing.Name);
    }

    [Test]
    public async Task FindOrCreate_WhenTenRunInParallel_ShouldCreateOneUser()
    {
        var calls = Enumerable.Range(0, 10).Select(_ => Task.Run(() => Directory.FindOrCreateAsync("racer@example.test", "Race Car")));

        var users = await Task.WhenAll(calls);

        users.Select(u => u.Id).Distinct().Count().ShouldBe(1);
        (await CountUsersWithEmailAsync("racer@example.test")).ShouldBe(1);
    }

    [TestCase("")]
    [TestCase("no-at-sign")]
    [TestCase("a@b@c.test")]
    [TestCase("has space@example.test")]
    public async Task FindOrCreate_WhenEmailInvalid_ShouldThrowValidation(string email)
    {
        var error = await Should.ThrowAsync<ApiException>(() => Directory.FindOrCreateAsync(email, "Some One"));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
    }
}
