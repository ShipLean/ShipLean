using System.Net;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.AccessSweep;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Access;

// No /api route ships without its access rule being exercised: swept, instance-admin, or allow-listed with a reason.
[TestFixture]
public class RouteCoverageTests : ApiFixture
{
    [Test]
    public void Routes_WhenUnderApi_ShouldBeSweptOrAllowListed()
    {
        var uncovered = ApiActions.Uncovered(ApiActions.Of(Host.Services), AccessSweepRows.All(), IsAllowListed);

        uncovered.Select(a => a.ToString()).ShouldBeEmpty();
    }

    [Test]
    public void Routes_WhenInstanceAdmin_ShouldHaveAnInstanceRow()
    {
        var missing = ApiActions.InstanceAdminWithoutRow(ApiActions.Of(Host.Services), AccessSweepRows.All());

        missing.Select(a => a.ToString()).ShouldBeEmpty();
    }

    [Test]
    public void Routes_WhenActionHasNoHttpVerb_ShouldFail()
    {
        ApiActions.Of(Host.Services).Where(a => a.Method == ApiAction.NoVerb).Select(a => a.Template).ShouldBeEmpty();
    }

    private static bool IsAllowListed(ApiAction action)
        => RouteAllowList.Contains(RouteAllowList.Anonymous, action) || RouteAllowList.Contains(RouteAllowList.SignedIn, action);

    [Test]
    public void Routes_WhenAllowListed_ShouldBeMappedAndUnique()
    {
        var actions = ApiActions.Of(Host.Services);
        var listed = RouteAllowList.Anonymous.Concat(RouteAllowList.SignedIn).ToList();

        listed.Where(e => !actions.Any(a => a.Method == e.Method && a.Template == e.Template)).ShouldBeEmpty();
        listed.GroupBy(e => (e.Method, e.Template)).Where(g => g.Count() > 1).ShouldBeEmpty();
        listed.Where(e => string.IsNullOrWhiteSpace(e.Reason)).ShouldBeEmpty();
    }

    [Test]
    public void Routes_WhenAllowListedWithAParameter_ShouldBeOnlyCallerOwnedRoutes()
    {
        var listed = RouteAllowList.Anonymous.Concat(RouteAllowList.SignedIn);

        RouteAllowList.WithParameters(listed).ShouldBeEmpty();
    }

    [Test]
    public void Rows_WhenTemplateHasNonProjectParameter_ShouldSetUrl()
    {
        ApiActions.RowsNeedingUrl(AccessSweepRows.All()).Select(r => $"{r.Method} {r.RouteTemplate}").ShouldBeEmpty();
    }

    [Test]
    public async Task Routes_WhenNoCookie_ShouldBeUnauthorizedUnlessAnonymous()
    {
        var anonymous = Api.ActingAs(TestUser.Anonymous);
        var answers = new List<string>();
        foreach (var action in ApiActions.Of(Host.Services).Where(a => a.Method != ApiAction.NoVerb))
        {
            var entry = RouteAllowList.Anonymous.FirstOrDefault(e => e.Method == action.Method && e.Template == action.Template);
            var allowed = entry is not null;
            using var response = await SweepCalls.SendAsync(
                anonymous, new AccessSweepRow(action.Method, action.Template, ShipLean.Contracts.ProjectRole.Member, entry?.Body));
            var unauthorized = response.StatusCode == HttpStatusCode.Unauthorized;
            if (allowed == unauthorized)
            {
                answers.Add($"{action}: {(int)response.StatusCode}, anonymous allowed: {allowed}");
            }
            else if (unauthorized)
            {
                (await ApiClient.ReadApiErrorAsync(response)).Code.ShouldBe(ApiErrorCodes.Unauthorized);
            }
        }

        answers.ShouldBeEmpty();
    }
}
