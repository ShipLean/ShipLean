using ShipLean.Application.Features.Timeline.Facts;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views.Timeline;
using ShipLean.TestSupport.Timeline;

namespace ShipLean.UnitTest.Timeline;

// The facts the pinned finished view (feature-timeline-view.json) is told from, viewed at AsOf in UTC. The bars and edges fixture
// tests both build from these, so the fixture is exactly what the builder makes.
internal static class TimelineFixtureFacts
{
    public static readonly DateTimeOffset AsOf = At("10-24T14:00");

    private static readonly Guid Priya = Person(1);
    private static readonly Guid Tom = Person(2);
    private static readonly Guid Mei = Person(3);
    private static readonly Guid Sam = Person(4);
    private static readonly Guid Ana = Person(5);
    private const string Agent = "Claude Code";

    private static Guid Person(int n) => new($"6f1c2a10-0000-4000-8000-00000000000{n}");

    public static DateTimeOffset At(string iso) => DateTimeOffset.Parse($"2026-{iso}:00Z", System.Globalization.CultureInfo.InvariantCulture);

    private static void Active(TimelineFactsBuilder f, string key, string at, string? agent = null) =>
        f.WithMove(key, At(at), "Active", StateCategory.InProgress, agent);

    private static void Resolved(TimelineFactsBuilder f, string key, string at) =>
        f.WithMove(key, At(at), "Resolved", StateCategory.Resolved);

    // The delivery the fixture draws, as facts: four stories and bugs, nine tasks, two blocked spells, ten reviews, two sprints, five
    // timed commits, eight links and two waves.
    public static TimelineFacts Build()
    {
        var f = new TimelineFactsBuilder()
            .WithFeature("SHIP-99", "Coupons v2", "Done", StateCategory.Completed, At("08-18T10:20"), Ana)
            .WithEpic("SHIP-80", "Checkout conversion")
            .WithPerson(Priya, "Priya Shah").WithPerson(Tom, "Tom Okafor").WithPerson(Mei, "Mei Lin").WithPerson(Sam, "Sam Rivera").WithPerson(Ana, "Ana Costa")
            .WithPlanApplied(At("10-01T16:45")).WithPlannedBy(agentName: Agent)
            .WithStory("SHIP-185", "Coupon rules engine", Tom, At("10-01T16:45"))
            .WithStory("SHIP-188", "Coupon admin page", Ana, At("10-01T16:45"))
            .WithStory("SHIP-186", "Apply coupon at checkout", Priya, At("10-01T16:45"))
            .WithBug("SHIP-189", "Coupon applied twice on double-click", Sam, At("10-20T08:00"))
            .WithTask("SHIP-191", "SHIP-185", "Rules schema and migration")
            .WithTask("SHIP-192", "SHIP-185", "Stacking rules (percent + fixed)")
            .WithTask("SHIP-193", "SHIP-185", "Expiry and usage limits")
            .WithTask("SHIP-197", "SHIP-188", "Admin list and create form", Ana)
            .WithTask("SHIP-198", "SHIP-188", "Bulk code import", Ana)
            .WithTask("SHIP-194", "SHIP-186", "API: validate and apply", Priya)
            .WithTask("SHIP-195", "SHIP-186", "UI: coupon field and errors", Mei)
            .WithTask("SHIP-196", "SHIP-186", "E2E: coupon checkout")
            .WithTask("SHIP-199", "SHIP-189", "Debounce apply and make it idempotent")
            .WithSprint("Sprint 41", new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 11))
            .WithSprint("Sprint 42", new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 25));

        Active(f, "SHIP-191", "10-05T09:10", Agent);
        Resolved(f, "SHIP-191", "10-05T11:40");
        Active(f, "SHIP-192", "10-06T10:30", Agent);
        Resolved(f, "SHIP-192", "10-06T14:00");
        Active(f, "SHIP-192", "10-07T09:30");
        Resolved(f, "SHIP-192", "10-07T11:15");
        Active(f, "SHIP-193", "10-06T10:40", Agent);
        Resolved(f, "SHIP-193", "10-06T13:10");
        Active(f, "SHIP-197", "10-06T13:00");
        Resolved(f, "SHIP-197", "10-08T11:00");
        f.WithMove("SHIP-198", At("10-16T17:30"), "Closed", StateCategory.Completed);
        Active(f, "SHIP-194", "10-08T09:00");
        f.WithBlockedTag("SHIP-194", At("10-08T15:00"), blockedAfter: true).WithBlockedTag("SHIP-194", At("10-12T10:00"), blockedAfter: false);
        Resolved(f, "SHIP-194", "10-12T16:00");
        Active(f, "SHIP-195", "10-14T13:00");
        Resolved(f, "SHIP-195", "10-15T12:00");
        Active(f, "SHIP-196", "10-19T11:00", Agent);
        f.WithBlockedTag("SHIP-196", At("10-19T13:30"), blockedAfter: true).WithBlockedTag("SHIP-196", At("10-20T14:40"), blockedAfter: false);
        Resolved(f, "SHIP-196", "10-20T16:00");
        Active(f, "SHIP-199", "10-20T09:00", Agent);
        Resolved(f, "SHIP-199", "10-20T10:20");

        f.WithReview("SHIP-191", At("10-06T10:05"), ReviewVerdict.Approved, Ana, Agent)
            .WithReview("SHIP-192", At("10-07T09:30"), ReviewVerdict.ChangesRequested, Ana, Agent, "Percent and fixed discounts apply in the wrong order when both match.")
            .WithReview("SHIP-192", At("10-07T16:20"), ReviewVerdict.Approved, Ana, Agent)
            .WithReview("SHIP-193", At("10-06T17:00"), ReviewVerdict.Approved, Ana, Agent)
            .WithReview("SHIP-197", At("10-09T11:00"), ReviewVerdict.Approved, Tom)
            .WithReview("SHIP-194", At("10-14T11:30"), ReviewVerdict.Approved, Ana, Agent)
            .WithReview("SHIP-195", At("10-19T10:15"), ReviewVerdict.Approved, Ana, Agent)
            .WithReview("SHIP-196", At("10-21T09:40"), ReviewVerdict.Approved, Ana, Agent)
            .WithReview("SHIP-199", At("10-20T14:30"), ReviewVerdict.Approved, Ana, Agent);
        f.WithMove("SHIP-99", At("10-21T10:05"), "Done", StateCategory.Completed, userId: Priya);
        f.WithCommit("SHIP-191", At("10-05T11:30")).WithCommit("SHIP-192", At("10-06T13:40")).WithCommit("SHIP-192", At("10-07T11:05"))
            .WithCommit("SHIP-194", At("10-12T15:20")).WithCommit("SHIP-196", At("10-20T15:45"));

        // The links the fixture's arrows come from, the wave that puts the bug fix after the checkout story, and the move back to work
        // once SHIP-199 ended, which is when SHIP-196 was unblocked.
        f.WithLink("SHIP-191", "SHIP-192").WithLink("SHIP-191", "SHIP-193").WithLink("SHIP-191", "SHIP-197")
            .WithLink("SHIP-192", "SHIP-194").WithLink("SHIP-193", "SHIP-194").WithLink("SHIP-194", "SHIP-195").WithLink("SHIP-195", "SHIP-196")
            .WithLink("SHIP-199", "SHIP-196", TimelineLinkKind.BlockedBy)
            .WithWave("SHIP-186", "1").WithWave("SHIP-189", "2");
        Active(f, "SHIP-196", "10-20T14:40", Agent);

        var facts = f.Build();
        return facts with { Feature = facts.Feature with { Id = new Guid("0b6f3c9e-99a0-4c1e-9d55-000000000099") } };
    }
}
