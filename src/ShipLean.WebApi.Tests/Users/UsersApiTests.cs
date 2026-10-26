using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Dapper;
using ShipLean.Application.Identity;
using ShipLean.Contracts.Refs;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Users;

// One seeded scenario: the seeded passwords verify through Passwords, the UserRef fragment reads the pinned refs, and
// GET /api/users/by-email answers an exact email only.
[TestFixture]
public class UsersApiTests : ApiFixture
{
    private Guid _withPhoto;

    [OneTimeSetUp]
    public async Task ArrangeUserWithAPhoto()
    {
        await using var connection = await OpenDatabaseAsync();
        _withPhoto = await new AppUserBuilder().WithEmail("photo.person@example.test").WithName("Photo Person").WithPhoto().InsertAsync(connection);
    }

    [Test]
    public async Task SeedPasswords_WhenVerified_ShouldMatch()
    {
        var passwords = new Passwords();
        var fields = typeof(SeedIds.Passwords).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.IsLiteral).ToList();
        fields.ShouldNotBeEmpty();
        await using var connection = await OpenDatabaseAsync();

        foreach (var field in fields)
        {
            var hash = await connection.QuerySingleAsync<string>(
                "SELECT password_hash FROM app_user WHERE lower(email) = @email", new { email = $"{field.Name.ToLowerInvariant()}@shiplean.local" });
            passwords.Verify(hash, (string)field.GetRawConstantValue()!).ShouldBeTrue(field.Name);
            passwords.Verify(hash, "wrong-" + field.GetRawConstantValue()).ShouldBeFalse(field.Name);
        }
    }

    [Test]
    public async Task UserRefs_WhenSelected_ShouldMatchThePinnedRefs()
    {
        await using var connection = await OpenDatabaseAsync();
        var json = await File.ReadAllTextAsync(Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "PinnedShapes", "refs.json"));
        var pinned = JsonDocument.Parse(json).RootElement.GetProperty("userRefs").Deserialize<UserRef[]>(JsonSerializerOptions.Web)!;

        var refs = (await connection.QueryAsync<UserRef>(
            $"SELECT {UserRefs.Columns} FROM app_user u WHERE u.id = ANY(@ids) ORDER BY u.id", new { ids = pinned.Select(p => p.Id).ToArray() })).ToList();

        refs.Count.ShouldBe(pinned.Length);
        foreach (var expected in pinned)
        {
            refs.Single(r => r.Id == expected.Id).ShouldBe(expected);
        }
    }

    [Test]
    public async Task UserRefs_WhenUserHasAPhoto_ShouldGiveThePhotoUrl()
    {
        await using var connection = await OpenDatabaseAsync();

        var photo = await connection.QuerySingleAsync<UserRef>($"SELECT {UserRefs.Columns} FROM app_user u WHERE u.id = @id", new { id = _withPhoto });

        photo.PhotoUrl.ShouldBe($"/api/users/{_withPhoto}/photo?v=1");
        photo.Initials.ShouldBe("PP");
    }

    [Test]
    public async Task ByEmail_WhenExact_ShouldReturnUserRef()
    {
        var response = await Api.GetAsync("/api/users/by-email?email=" + Uri.EscapeDataString(" Photo.Person@Example.test "));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.Deserialize<UserRef>(JsonSerializerOptions.Web).ShouldBe(new UserRef(_withPhoto, "Photo Person", "PP", "#2f7fd8", $"/api/users/{_withPhoto}/photo?v=1"));
        body.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ShouldBe(["avatarColour", "id", "initials", "name", "photoUrl"]);
    }

    [Test]
    public async Task ByEmail_WhenPartial_ShouldBeNotFound()
    {
        var response = await Api.GetAsync("/api/users/by-email?email=photo.person");

        var error = await response.ReadApiErrorAsync(HttpStatusCode.NotFound);
        error.Code.ShouldBe("not_found");
    }
}
