using System.Net;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport.AccessSweep;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Auth;

namespace ShipLean.WebApi.Tests.Access;

// The sweep's instance and below-minimum legs have no real row yet, so they are proved here: a host that loads the test
// assembly's AuthTestController ([InstanceAdmin] GET api/test-auth/admin) swept with a local instance row. If a leg
// stopped checking, or produced no cases, these fail.
[TestFixture]
public class SweepSelfTests : ApiFixture
{
    private static readonly AccessSweepRow[] LocalRows = [new("GET", "api/test-auth/admin", ProjectRole.Admin, InstanceScoped: true)];

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
        services.AddControllers().AddApplicationPart(typeof(AuthTestController).Assembly);

    public static IEnumerable<TestCaseData> Rows() => SweepCalls.Cases(LocalRows);

    [Test]
    public void Rows_WhenLocalRowsListed_ShouldEachMatchARoute()
    {
        ApiActions.Unmatched(LocalRows, ApiActions.Of(Host.Services)).ShouldBeEmpty();
    }

    [TestCaseSource(nameof(Rows))]
    public async Task AccessSweep_WhenInstanceRowAndNotAdmin_ShouldBeForbidden(AccessSweepRow row)
    {
        using var response = await SweepCalls.SendAsync(Api.ActingAs(TestUser.Mei), row);

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Forbidden)).Code.ShouldBe(ApiErrorCodes.Forbidden);
    }

    [TestCaseSource(nameof(Rows))]
    public async Task AccessSweep_WhenBelowMinimum_ShouldBeForbidden(AccessSweepRow row)
    {
        using var response = await SweepCalls.SendAsync(Api.ActingAs(TestUser.Mei), row);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [TestCaseSource(nameof(Rows))]
    public async Task AccessSweep_WhenInstanceAdmin_ShouldPassAccess(AccessSweepRow row)
    {
        using var response = await SweepCalls.SendAsync(Api.ActingAs(TestUser.Joel), row);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
