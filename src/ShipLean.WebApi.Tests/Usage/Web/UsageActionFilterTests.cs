using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using ShipLean.Contracts.Usage;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Usage;
using ShipLean.WebApi.Features.Boards.View;
using ShipLean.WebApi.Features.Usage;

namespace ShipLean.WebApi.Tests.Usage.Web;

// The filter on its own, with a hand-filled IUsageScope: no production action adds a property yet, and test-only actions are
// banned from the map, so the merge is exercised here on a real Track entry (BoardView.Move, which carries board).
[TestFixture]
public class UsageActionFilterTests
{
    [Test]
    public async Task Filter_WhenScopeHasProperties_ShouldRecordThemWithBoardAndViewport()
    {
        var events = new RecordingUsageEvents();
        var scope = new FakeUsageScope { Viewport = UsageViewports.Phone };
        scope.AddProperty(UsageProps.Count, 3L);
        var filter = new UsageActionFilter(events, scope, TimeProvider.System);
        var (executing, executed) = Contexts(nameof(BoardViewController.Move));

        await filter.OnActionExecutionAsync(executing, () => Task.FromResult(executed));

        var tracked = events.Events.ShouldHaveSingleItem();
        tracked.Source.ShouldBe("api:BoardView.Move");
        tracked.Properties.Count.ShouldBe(3);
        tracked.Properties[UsageProps.Board].ShouldBe("board");
        tracked.Properties[UsageProps.Count].ShouldBe(3L);
        tracked.Properties[UsageProps.Viewport].ShouldBe(UsageViewports.Phone);
    }

    private static (ActionExecutingContext Executing, ActionExecutedContext Executed) Contexts(string action)
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, SeedIds.Users.Joel.ToString())], "test")),
        };
        var descriptor = new ControllerActionDescriptor
        {
            ControllerTypeInfo = typeof(BoardViewController).GetTypeInfo(),
            MethodInfo = typeof(BoardViewController).GetMethod(action)!,
            ControllerName = "BoardView",
            ActionName = action,
        };
        var actionContext = new ActionContext(http, new RouteData(), descriptor);
        var executing = new ActionExecutingContext(actionContext, [], new Dictionary<string, object?>(), controller: new object());
        var executed = new ActionExecutedContext(actionContext, [], controller: new object()) { Result = new OkResult() };
        return (executing, executed);
    }
}
