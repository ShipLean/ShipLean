using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Features.Mcp.Plan;

namespace ShipLean.WebApi.Tests.Mcp.Plan;

// The sealed plan on the app's Data Protection, with the clock pinned so expiry is a matter of advancing it.
[TestFixture]
public class PlanTests : ApiFixture
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 24, 9, 0, 0, TimeSpan.Zero));

    protected override Action<IServiceCollection>? ConfigureTestServices => services => services.AddSingleton<TimeProvider>(_clock);

    private const string Kind = "plan_sprint";

    private PlanEnvelope Envelope => Host.Services.GetRequiredService<PlanEnvelope>();

    private static PlanBody Body() => new(SeedIds.Sprints.Sprint25.ToString(), [new PlanItem(SeedIds.WorkItems.Ship90, 3), new PlanItem(SeedIds.WorkItems.Ship91, 1)]);

    [Test]
    public void Plan_WhenRoundTripped_ShouldReturnPayload()
    {
        var plan = Envelope.Seal(Kind, SeedIds.Projects.Ship, Body());

        Envelope.Open<PlanBody>(Kind, plan, SeedIds.Projects.Ship).ShouldBe(Body(), new PlanBodyComparer());
    }

    [Test]
    public void Plan_WhenTampered_ShouldBeValidation()
    {
        var plan = Envelope.Seal(Kind, SeedIds.Projects.Ship, Body());
        var tampered = plan[..^4] + (plan.EndsWith("AAAA", StringComparison.Ordinal) ? "BBBB" : "AAAA");

        var tamperedError = Should.Throw<ApiException>(() => Envelope.Open<PlanBody>(Kind, tampered, SeedIds.Projects.Ship));
        var garbageError = Should.Throw<ApiException>(() => Envelope.Open<PlanBody>(Kind, "not a plan", SeedIds.Projects.Ship));

        new[] { tamperedError, garbageError }.ShouldAllBe(e => e.Code == ApiErrorCodes.Validation && e.Field == "plan");
    }

    [Test]
    public void Plan_WhenExpired_ShouldBeConflict()
    {
        var plan = Envelope.Seal(Kind, SeedIds.Projects.Ship, Body());
        _clock.Advance(PlanEnvelope.Lifetime + TimeSpan.FromSeconds(1));

        var error = Should.Throw<ApiException>(() => Envelope.Open<PlanBody>(Kind, plan, SeedIds.Projects.Ship));

        error.Code.ShouldBe(ApiErrorCodes.Conflict);
        error.Message.ShouldContain("expired");
    }

    [Test]
    public void Plan_WhenJustInsideTheLifetime_ShouldOpen()
    {
        var plan = Envelope.Seal(Kind, SeedIds.Projects.Ship, Body());
        _clock.Advance(PlanEnvelope.Lifetime - TimeSpan.FromSeconds(1));

        Envelope.Open<PlanBody>(Kind, plan, SeedIds.Projects.Ship).Target.ShouldBe(Body().Target);
    }

    [Test]
    public void Plan_WhenOtherUser_ShouldBeValidation()
    {
        var plan = Envelope.Seal(Kind, SeedIds.Projects.Ship, Body());
        Host.ActAs(SeedIds.Users.Priya);

        var error = Should.Throw<ApiException>(() => Envelope.Open<PlanBody>(Kind, plan, SeedIds.Projects.Ship));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("plan");
    }

    [Test]
    public void Plan_WhenOtherProject_ShouldBeValidation()
    {
        var plan = Envelope.Seal(Kind, SeedIds.Projects.Ship, Body());

        var error = Should.Throw<ApiException>(() => Envelope.Open<PlanBody>(Kind, plan, SeedIds.Projects.Pay));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public void Plan_WhenOtherKind_ShouldBeValidation()
    {
        var plan = Envelope.Seal(Kind, SeedIds.Projects.Ship, Body());

        var error = Should.Throw<ApiException>(() => Envelope.Open<PlanBody>("end_sprint", plan, SeedIds.Projects.Ship));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("plan");
    }

    [Test]
    public void Plan_WhenNull_ShouldBeValidation()
    {
        var nullError = Should.Throw<ApiException>(() => Envelope.Open<PlanBody>(Kind, null, SeedIds.Projects.Ship));
        var blankError = Should.Throw<ApiException>(() => Envelope.Open<PlanBody>(Kind, "  ", SeedIds.Projects.Ship));

        new[] { nullError, blankError }.ShouldAllBe(e => e.Code == ApiErrorCodes.Validation && e.Field == "plan");
    }
}
