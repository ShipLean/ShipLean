using System.Net;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport.AccessSweep;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Access;

// Instance-scoped rows need an instance admin: Mei is a member of SHIP but not an instance admin. (Joel, who is one,
// passes them in AccessSweep_WhenAllowed_ShouldPassAccess.)
[TestFixture]
public class InstanceSweepTests : ApiFixture
{
    [TestCaseSource(typeof(SweepCalls), nameof(SweepCalls.InstanceRows))]
    public async Task AccessSweep_WhenInstanceRowAndNotAdmin_ShouldBeForbidden(AccessSweepRow row)
    {
        using var response = await SweepCalls.SendAsync(Api.ActingAs(TestUser.Mei), row);

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Forbidden);
        error.Code.ShouldBe(ApiErrorCodes.Forbidden);
    }
}
