using ShipLean.Application.Features.InspectionMetrics.Facts;
using ShipLean.Application.Features.InspectionMetrics.Periods;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views.InspectionMetrics;
using ShipLean.Contracts.Views.Timeline;

namespace ShipLean.TestSupport.InspectionMetrics;

// The test double of K5: a fluent builder of LeadTimeFacts for the measure's and the aggregator's tests, and Sample(), the design's
// sample history (Sprints 16 to 24, design/screens/inspection-data.js). A task whose state isn't given takes the state of its
// latest move, or New when it has none. A move or Blocked tag change whose revision isn't given takes the task's next revision,
// counted per task from 2 (revision 1 is the creation).
public sealed class LeadTimeFactsBuilder
{
    // Monday 5 October 2026, 00:00 UTC.
    public static readonly DateTimeOffset Start = new(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);

    public const string InProgressState = "In progress";
    public const string ResolvedState = "Resolved";

    // The sample: nine sprints, 123 measured tasks (the 8 hand-written ones of SHIP-99 Coupons v2 and 115 generated), and SHIP-198,
    // closed in a bulk edit with no start recorded, which the design lists as left out.
    public const int SampleSprintCount = 9;
    public const int SampleTaskCount = 123;
    public const string SampleLeftOutKey = "SHIP-198";

    private readonly List<(string Key, string Title, string? ParentKey, InspectionParentType? ParentType, string? FeatureKey, string? FeatureTitle, Guid? AssigneeId, string? AssigneeName, string? AgentName, string? State, StateCategory? Category)> _tasks = [];
    private readonly List<LeadTimeMoveFact> _moves = [];
    private readonly List<LeadTimeBlockedTagFact> _blockedTags = [];
    private readonly List<LeadTimeReviewFact> _reviews = [];
    private readonly List<LeadTimeCommitFact> _commits = [];
    private readonly Dictionary<Guid, string> _people = [];
    private readonly Dictionary<string, int> _revs = new(StringComparer.Ordinal);

    public LeadTimeFactsBuilder WithTask(
        string key, string title = "Task", string? parentKey = "SHIP-300", InspectionParentType? parentType = InspectionParentType.Story,
        string? featureKey = "SHIP-99", string? featureTitle = "Coupons v2", Guid? assigneeId = null, string? assigneeName = null,
        string? agentName = null, string? stateName = null, StateCategory? category = null)
    {
        _tasks.Add((key, title, parentKey, parentType, featureKey, featureTitle, assigneeId, assigneeName, agentName, stateName, category));
        return this;
    }

    // A move of the task to a state at a time, by a user, through an agent when agentName is set.
    public LeadTimeFactsBuilder WithMove(
        string taskKey, DateTimeOffset at, string stateName, StateCategory category, string? agentName = null, Guid? userId = null, int? rev = null)
    {
        _moves.Add(new LeadTimeMoveFact(taskKey, at, rev ?? NextRev(taskKey), stateName, category, agentName, userId));
        return this;
    }

    // The task's first move to an In-progress state (its start), by a user or through an agent.
    public LeadTimeFactsBuilder WithStart(string taskKey, DateTimeOffset at, string? agentName = null, Guid? userId = null)
        => WithMove(taskKey, at, InProgressState, StateCategory.InProgress, agentName, userId);

    // A move of the task to a Resolved-category state.
    public LeadTimeFactsBuilder WithResolved(string taskKey, DateTimeOffset at, string? agentName = null, Guid? userId = null)
        => WithMove(taskKey, at, ResolvedState, StateCategory.Resolved, agentName, userId);

    // The Blocked tag added (blockedAfter true) or removed (false) at a time.
    public LeadTimeFactsBuilder WithBlockedTag(string taskKey, DateTimeOffset at, bool blockedAfter, int? rev = null)
    {
        _blockedTags.Add(new LeadTimeBlockedTagFact(taskKey, at, rev ?? NextRev(taskKey), blockedAfter));
        return this;
    }

    // The Blocked tag on from one time and off at another.
    public LeadTimeFactsBuilder WithBlocked(string taskKey, DateTimeOffset from, DateTimeOffset to)
        => WithBlockedTag(taskKey, from, true).WithBlockedTag(taskKey, to, false);

    public LeadTimeFactsBuilder WithReview(string taskKey, DateTimeOffset at, ReviewVerdict verdict)
    {
        _reviews.Add(new LeadTimeReviewFact(taskKey, at, verdict));
        return this;
    }

    // A commit on the task; committedAt null is a commit with no stored time.
    public LeadTimeFactsBuilder WithCommit(string taskKey, string sha7, DateTimeOffset? committedAt)
    {
        _commits.Add(new LeadTimeCommitFact(taskKey, sha7, committedAt));
        return this;
    }

    public LeadTimeFactsBuilder WithPerson(Guid userId, string name)
    {
        _people[userId] = name;
        return this;
    }

    public LeadTimeFacts Build()
        => new(
            [.. _tasks.Select(t =>
            {
                var (state, category) = Current(t.Key, t.State, t.Category);
                return new LeadTimeTaskFact(t.Key, t.Title, t.ParentKey, t.ParentType, t.FeatureKey, t.FeatureTitle, t.AssigneeId, t.AssigneeName, t.AgentName, state, category);
            })],
            [.. _moves],
            [.. _blockedTags],
            [.. _reviews],
            [.. _commits],
            new Dictionary<Guid, string>(_people));

    // The design's sample sprints: Sprints 16 to 24, two weeks each from Monday 29 June 2026, ending on the Friday of their second week.
    public static IReadOnlyList<InspectionSprint> SampleSprints()
        => [.. Enumerable.Range(16, SampleSprintCount).Select(n =>
        {
            var start = new DateOnly(2026, 6, 29).AddDays((n - 16) * 14);
            return new InspectionSprint(SampleSprintId(n), $"Sprint {n}", start, start.AddDays(11));
        })];

    public static Guid SampleSprintId(int sprintNumber) => new($"00000000-0000-4000-8000-0000000{sprintNumber:D5}");

    // The design's sample history as facts: the people are Person guids from SamplePerson. "Now" in the design is Sat 24 Oct 2026, 14:00.
    public static LeadTimeFacts Sample()
    {
        var builder = new LeadTimeFactsBuilder();
        var sample = new SampleHistory();
        sample.Fill(builder);
        return builder.Build();
    }

    public static Guid SamplePerson(string who) => new($"00000000-0000-4000-8000-1000000000{Array.IndexOf(SampleHistory.Who, who) + 1:D2}");

    private int NextRev(string taskKey)
    {
        var rev = _revs.GetValueOrDefault(taskKey, 1) + 1;
        _revs[taskKey] = rev;
        return rev;
    }

    private (string State, StateCategory Category) Current(string key, string? state, StateCategory? category)
    {
        if (state is not null && category is { } given)
        {
            return (state, given);
        }

        var latest = _moves.Where(m => m.ItemKey == key).OrderBy(m => m.At).ThenBy(m => m.Rev).LastOrDefault();
        return latest is null ? ("New", StateCategory.Proposed) : (latest.StateName, latest.Category);
    }

    // The port of inspection-data.js: its linear congruential generator is ported with it, so the generated rows are the design's.
    private sealed class SampleHistory
    {
        public static readonly string[] Who = ["claude", "cursor", "gemini", "ana", "priya", "mei", "tom", "sam"];

        private static readonly string[] Agents = ["claude", "cursor", "gemini"];
        private static readonly string[] AgentNames = ["Claude Code", "Cursor", "Gemini CLI"];
        private static readonly string[] PersonNames = ["Ana", "Priya", "Mei", "Tom", "Sam"];

        private static readonly Dictionary<string, string> Features = new()
        {
            ["SHIP-99"] = "Coupons v2", ["SHIP-101"] = "Checkout v2", ["SHIP-102"] = "Mobile sign-in", ["SHIP-103"] = "Reporting",
            ["SHIP-108"] = "Dashboard widgets", ["SHIP-104"] = "Ops hygiene", ["SHIP-96"] = "Search relevance", ["SHIP-95"] = "Order history",
        };

        private static readonly string[] Titles =
        [
            "API endpoint and contract test", "Migration and backfill", "UI: form and validation", "E2E happy path", "Feature flag and rollout",
            "Error states and retries", "Telemetry events", "Copy and empty states", "Accessibility pass", "Load test and index",
            "Contract tests against the gateway", "Settings page section", "Background job and alerting", "Docs and runbook", "Refactor the shared client",
        ];

        // Which Features were running in which sprints, by sprint number.
        private static readonly Dictionary<int, string[]> Running = new()
        {
            [16] = ["SHIP-95", "SHIP-96"], [17] = ["SHIP-95", "SHIP-96"], [18] = ["SHIP-96", "SHIP-103"], [19] = ["SHIP-96", "SHIP-103", "SHIP-104"],
            [20] = ["SHIP-103", "SHIP-102", "SHIP-104"], [21] = ["SHIP-103", "SHIP-102", "SHIP-101"], [22] = ["SHIP-101", "SHIP-102", "SHIP-99"],
            [23] = ["SHIP-99", "SHIP-101", "SHIP-102"], [24] = ["SHIP-101", "SHIP-102", "SHIP-108"],
        };

        // Per sprint: the generated task count and the lead-time target in hours.
        private static readonly (int Sprint, int Count, int Target)[] Shape =
            [(16, 11, 34), (17, 12, 31), (18, 12, 33), (19, 13, 27), (20, 14, 29), (21, 15, 24), (22, 16, 21), (23, 13, 20), (24, 9, 15)];

        private static readonly DateTimeOffset Now = At(24, 10, 14, 0);

        private ulong _seed = 1398;

        public void Fill(LeadTimeFactsBuilder builder)
        {
            for (var i = 0; i < Who.Length; i++)
            {
                builder.WithPerson(SamplePerson(Who[i]), Name(Who[i]));
            }

            HandWritten(builder);
            var generated = Generate();
            var shas = generated.Select(_ => Sha()).ToList();
            for (var i = 0; i < generated.Count; i++)
            {
                Add(builder, generated[i] with { Commits = [(shas[i], generated[i].Commits[0].At)] });
            }

            // SHIP-198 was closed in a bulk edit with no start recorded: a Resolved move and nothing before it.
            builder.WithTask(SampleLeftOutKey, "Bulk code import", "SHIP-185", InspectionParentType.Story, "SHIP-99", "Coupons v2", SamplePerson("ana"), "Ana");
            builder.WithResolved(SampleLeftOutKey, At(16, 10, 17, 30), userId: SamplePerson("ana"));
        }

        private static DateTimeOffset At(int day, int month, int hour, int minute) => new(2026, month, day, hour, minute, 0, TimeSpan.Zero);

        private static string Name(string who)
        {
            var agent = Array.IndexOf(Agents, who);
            return agent >= 0 ? AgentNames[agent] : PersonNames[Array.FindIndex(PersonNames, n => n.Equals(who, StringComparison.OrdinalIgnoreCase))];
        }

        private static bool IsAgent(string who) => Array.IndexOf(Agents, who) >= 0;

        private void HandWritten(LeadTimeFactsBuilder builder)
        {
            // key, parent, title, who, start, last commit, resolved, blocked hours, rework hours, reviews
            var rows = new (string Key, string Parent, string Title, string Who, DateTimeOffset Start, DateTimeOffset Commit, DateTimeOffset Resolved, double Blocked, double Rework, int Reviews)[]
            {
                ("SHIP-191", "SHIP-185", "Rules schema and migration", "claude", At(5, 10, 9, 10), At(5, 10, 11, 32), At(5, 10, 11, 40), 0, 0, 1),
                ("SHIP-192", "SHIP-185", "Stacking rules (percent + fixed)", "claude", At(6, 10, 10, 30), At(7, 10, 11, 5), At(6, 10, 14, 0), 0, 21.1, 2),
                ("SHIP-193", "SHIP-185", "Expiry and usage limits", "claude", At(6, 10, 10, 40), At(6, 10, 13, 2), At(6, 10, 13, 10), 0, 0, 1),
                ("SHIP-197", "SHIP-188", "Admin list and create form", "ana", At(6, 10, 13, 0), At(8, 10, 10, 40), At(8, 10, 11, 0), 0, 0, 1),
                ("SHIP-194", "SHIP-186", "API: validate and apply", "priya", At(8, 10, 9, 0), At(12, 10, 15, 20), At(12, 10, 16, 0), 91, 0, 1),
                ("SHIP-195", "SHIP-186", "UI: coupon field and errors", "mei", At(14, 10, 13, 0), At(15, 10, 11, 40), At(15, 10, 12, 0), 0, 0, 1),
                ("SHIP-196", "SHIP-186", "E2E: coupon checkout", "claude", At(19, 10, 11, 0), At(20, 10, 15, 48), At(20, 10, 16, 0), 25.2, 0, 1),
                ("SHIP-199", "SHIP-189", "Debounce apply and make it idempotent", "claude", At(20, 10, 9, 0), At(20, 10, 10, 12), At(20, 10, 10, 20), 0, 0, 1),
            };
            for (var i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                // SHIP-192's rework is the time from its first resolve to its last commit, so its first pass is the earlier commit.
                var commits = new List<(string Sha, DateTimeOffset? At)> { ("9f3c2a1", row.Commit) };
                if (row.Rework > 0)
                {
                    commits.Add(("b71e0d4", row.Commit.AddHours(-4)));
                }
                Add(builder, new SampleTask(
                    row.Key, row.Parent, row.Parent == "SHIP-189" ? InspectionParentType.Bug : InspectionParentType.Story, row.Title, "SHIP-99", row.Who,
                    row.Start, commits, row.Resolved, row.Blocked, row.Rework, row.Reviews));
            }
        }

        private List<SampleTask> Generate()
        {
            var tasks = new List<SampleTask>();
            var keyNo = 400;
            for (var s = 0; s < Shape.Length; s++)
            {
                var (number, count, target) = Shape[s];
                var sprintStart = At(29, 6, 0, 0).AddDays((number - 16) * 14);
                var sprintEnd = sprintStart.AddDays(11).AddHours(23);
                var current = sprintStart <= Now && Now <= sprintEnd;
                var lastDay = current ? Now.AddHours(-2) : sprintEnd.AddHours(-8);
                for (var i = 0; i < count; i++)
                {
                    tasks.Add(GenerateTask(number, target, sprintStart, lastDay, keyNo++));
                }
            }

            return tasks;
        }

        private SampleTask GenerateTask(int number, int target, DateTimeOffset sprintStart, DateTimeOffset lastDay, int keyNo)
        {
            var agent = Rnd() < (number - 10) / 14d;
            var who = agent ? Pick(["claude", "claude", "cursor", "gemini"]) : Pick(["priya", "tom", "mei", "sam", "ana"]);
            var lead = agent ? 1.5 + (Rnd() * target * 0.6) : target * (0.6 + (Rnd() * 1.4));
            double blocked = 0;
            double rework = 0;
            if (Rnd() < 0.12)
            {
                blocked = 8 + (Rnd() * 30);
                lead += blocked;
                Pick(["Waiting on a staging environment", "Waiting on a decision from the owner", "Upstream API change not deployed"]);
            }
            if (Rnd() < 0.25)
            {
                rework = 2 + (Rnd() * 14);
                lead += rework;
            }

            var end = sprintStart.AddHours(6 + (Rnd() * ((lastDay - sprintStart).TotalHours - 6)));
            var start = end.AddHours(-lead);
            var reported = number >= 22 ? Rnd() < 0.9 : Rnd() < 0.8;
            var tail = 0.2 + (Rnd() * (agent ? 3 : 10));
            var feature = Pick(Running[number]);
            var bug = Rnd() < 0.15;
            var title = Pick(Titles);
            var reviews = agent ? 1 + (rework > 0 ? 1 : 0) : (Rnd() < 0.7 ? 1 : 0);
            Rnd(); // the design's runs per task, drawn here and not used

            // The design's "resolved" source is the one whose commit has no time: it ends at the first Resolved.
            var commit = reported ? end : (DateTimeOffset?)null;
            var resolved = reported ? end.AddHours(tail) : end;
            return new SampleTask(
                $"SHIP-{keyNo}", $"SHIP-{300 + (keyNo % 60)}", bug ? InspectionParentType.Bug : InspectionParentType.Story, title, feature, who,
                start, [(string.Empty, commit)], resolved, blocked, rework, reviews);
        }

        private static void Add(LeadTimeFactsBuilder builder, SampleTask task)
        {
            var agent = IsAgent(task.Who);
            var person = SamplePerson(task.Who);
            builder.WithTask(
                task.Key, task.Title, task.Parent, task.ParentType, task.Feature, Features[task.Feature], person, Name(task.Who),
                agent ? Name(task.Who) : null, ResolvedState, StateCategory.Resolved);
            builder.WithMove(task.Key, task.Start, InProgressState, StateCategory.InProgress, agent ? Name(task.Who) : null, person);
            if (task.Blocked > 0)
            {
                builder.WithBlocked(task.Key, task.Start.AddHours(2), task.Start.AddHours(2 + task.Blocked));
            }

            var lastCommit = task.Commits.Max(c => c.At) ?? task.Resolved;
            if (task.Rework > 0)
            {
                builder.WithReview(task.Key, lastCommit.AddHours(-task.Rework), ReviewVerdict.ChangesRequested);
            }
            if (task.Reviews > 0)
            {
                builder.WithReview(task.Key, task.Resolved.AddMinutes(30), ReviewVerdict.Approved);
            }

            builder.WithResolved(task.Key, task.Resolved, agent ? Name(task.Who) : null, person);
            for (var i = 0; i < task.Commits.Count; i++)
            {
                builder.WithCommit(task.Key, task.Commits[i].Sha, task.Commits[i].At);
            }
        }

        private string Pick(string[] options) => options[(int)Math.Floor(Rnd() * options.Length)];

        private string Sha() => ((long)Math.Floor(Rnd() * 0xfffffff)).ToString("x").PadLeft(7, '0');

        private double Rnd()
        {
            _seed = ((_seed * 1664525) + 1013904223) % 4294967296;
            return _seed / 4294967296d;
        }

        private sealed record SampleTask(
            string Key, string Parent, InspectionParentType ParentType, string Title, string Feature, string Who, DateTimeOffset Start,
            IReadOnlyList<(string Sha, DateTimeOffset? At)> Commits, DateTimeOffset Resolved, double Blocked, double Rework, int Reviews);
    }
}
