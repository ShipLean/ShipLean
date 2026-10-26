using ShipLean.Contracts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.AccessSweep;

namespace ShipLean.WebApi.Tests.Users.Admin;

// The instance Users routes: instance admin only. The state-changing rows act on Sam, whom no other row touches, with
// bodies that are harmless on a second run.
public sealed class UsersAdminRows : IAccessSweepRows
{
    private static string Sam(string tail) => $"api/admin/users/{SeedIds.Users.Sam}/{tail}";

    public IEnumerable<AccessSweepRow> Rows =>
    [
        new("GET", "api/admin/users", ProjectRole.Member, InstanceScoped: true),
        new("POST", "api/admin/users", ProjectRole.Member, Body: """{"email":"sweep.added@example.test","name":"Sweep Added","projectIds":[]}""", InstanceScoped: true),
        new("POST", "api/admin/users/{id:guid}/one-time-password", ProjectRole.Member, Url: Sam("one-time-password"), InstanceScoped: true),
        new("POST", "api/admin/users/{id:guid}/unlock", ProjectRole.Member, Url: Sam("unlock"), InstanceScoped: true),
        new("PUT", "api/admin/users/{id:guid}/disabled", ProjectRole.Member, Url: Sam("disabled"), Body: """{"disabled":false}""", InstanceScoped: true),
        new("PUT", "api/admin/users/{id:guid}/instance-admin", ProjectRole.Member, Url: Sam("instance-admin"), Body: """{"isInstanceAdmin":false}""", InstanceScoped: true),
        new("PUT", "api/admin/users/{id:guid}/email", ProjectRole.Member, Url: Sam("email"), Body: """{"email":"sam@shiplean.local"}""", InstanceScoped: true),
    ];
}
