using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using ShipLean.Application;
using ShipLean.Contracts.Prompts;

namespace ShipLean.WebApi.Features.Prompts;

// The one reader of the embedded built-in prompts. Everything else asks IBuiltInPrompts, or IPromptResolver when an agent gets the text.
public sealed partial class BuiltInPrompts : IBuiltInPrompts
{
    private const string SprintDemoResource = "sprint-demo/sprint-demo-prompt.md";

    private static readonly IReadOnlyList<string> None = [];

    private static readonly IReadOnlyList<PromptStageFacts> StageList =
    [
        new(PromptStage.Intake, "Intake", "A GitHub issue arrives on a connected repository."),
        new(PromptStage.Shape, "Shape", "An idea or research question might become work."),
        new(PromptStage.Plan, "Plan", "A Feature or Epic is broken down into stories and tasks."),
        new(PromptStage.Build, "Build", "Each wave runs: leads and implementers pick up their briefs."),
        new(PromptStage.Review, "Review", "Each task’s commits are reviewed, then fixed."),
        new(PromptStage.Retro, "Retro", "The plan run ends and the conductor writes up what happened."),
        new(PromptStage.SprintDemo, "Sprint demo", "The sprint closes and someone asks for a demo video."),
    ];

    private static readonly IReadOnlyList<PromptBuiltInOnlyDto> BuiltInOnlyRows =
    [
        new("Sprint demo inserted parts (14 files: tools, models, ffmpeg, level, start, intro and return, each for handover and session)",
            "They depend on where the agent runs, so they stay built-in."),
        new("Server instructions", "Sent when the agent connects, before it picks a project. Same for every project, so not editable here."),
    ];

    private static readonly PromptFacts[] Facts_ =
    [
        new(PromptKey.IssueProposal, PromptStage.Intake, "issue-proposal.md", "Issue proposal",
            "When someone opens a GitHub issue on a connected repository, ShipLean starts an intake round. This is the agent’s session prompt: it restates the request, checks what already exists and posts a proposal on the issue.",
            "Issue intake round (agent-relay session)", "The intake agent, working in a read-only checkout",
            "Replaces the Proposal instructions box under Integrations. Its output is public, so keep the “Public text only” rules.", None, None),
        new(PromptKey.Work, PromptStage.Shape, "work.md", "Work from idea to done",
            "The first thing an agent reads when it gets an idea or a research question: find or create a Proposed Feature, put findings on it, and end with comment and set_status.",
            "planning_guide part work", "Any agent, at the start of a session", null, None, None),
        new(PromptKey.Guide, PromptStage.Plan, "guide.md", "Planning and breakdown",
            "Read before breaking a Feature or Epic down, whatever its size. It picks the mode (Direct, orchestrator or conductor), shapes waves and contracts, and walks the run through to the retro.",
            "planning_guide (default part)", "The conductor, before any breakdown", null, None, None),
        new(PromptKey.PlanReviewer, PromptStage.Plan, "plan_reviewer.md", "Plan reviewer",
            "After capture_plan and before wave 0: a fresh reviewer checks the plan against the four constraints (context size, no in-wave dependencies, flow, contracts first).",
            "planning_guide part plan_reviewer", "A reviewer from a different model family", null, None, None),
        new(PromptKey.Lead, PromptStage.Build, "lead.md", "Lead brief",
            "In conductor mode, each lane lead gets this when it calls get_brief on its story. It launches sub-agents, runs reviews and keeps the plan current; it never touches code.",
            "get_brief on a story (conductor mode)", "The lead of one lane", null, PlanPlaceholders.Lead, None),
        new(PromptKey.Brief, PromptStage.Build, "brief.md", "Task brief",
            "The wrapper around every task brief: ShipLean fills in the common brief, the task’s own brief, contracts and the shared branch. The implementer follows it from pick_up to report_task.",
            "get_brief on a task", "The implementer, alone in its worktree", null, PlanPlaceholders.Brief, None),
        new(PromptKey.Report, PromptStage.Build, "report.md", "Report",
            "How to fill report_task: status, commits, answered comments, tests run and acceptance tests. Inserted at the end of the Task brief.",
            "Appended to every task brief ({{report_template}})", "The implementer, when it stops", null, None, None),
        new(PromptKey.CodeReviewer, PromptStage.Review, "code_reviewer.md", "Code reviewer",
            "After an implementer reports, a reviewer reads only that task’s commits on the shared branch, comments Review · and calls log_review.",
            "planning_guide part code_reviewer", "A reviewer from a different model family", null, None, None),
        new(PromptKey.Fix, PromptStage.Review, "fix.md", "Fix round",
            "When log_review says changes_requested: fix each blocking finding with new commits, never amend or force-push, then report again.",
            "planning_guide part fix", "The implementer, after changes are requested", null, None, None),
        new(PromptKey.Retro, PromptStage.Retro, "retro.md", "Run retro",
            "The template for the run retro: budget, timeline table, what slowed it down. It feeds log_retrospective and, with SHIP-568, the project’s Ways of working.",
            "planning_guide part retro", "The conductor, at the end of the run", null, None, None),
        new(PromptKey.RetroAnalysis, PromptStage.Retro, "retro_analysis.md", "Analyse retros",
            "The method for analysing a project's retro points in aggregate: export, rewrite, cluster, check what is already enforced, and record ranked fixes by tier.",
            "planning_guide part retro_analysis", "The agent running an aggregate retro analysis", null, None, None),
        new(PromptKey.SprintDemo, PromptStage.SprintDemo, "sprint-demo-prompt.md", "Sprint demo video",
            "When a sprint closes and a demo is requested, this is the whole recipe for the narrated video: chapters, narration, recording, upload.",
            "create_sprint_demo", "The demo agent (handover or agent-relay session)", null, SprintDemoPlaceholders(),
            ["intro", "tools", "models", "level", "ffmpeg", "return", "start"]),
    ];

    private static readonly ConcurrentDictionary<PromptKey, string> Texts = new();
    private static readonly ConcurrentDictionary<PromptKey, string> Hashes = new();

    public IReadOnlyList<PromptStageFacts> Stages => StageList;

    public IReadOnlyList<PromptFacts> All => Facts_;

    public string ServerInstructions => PromptDefaults.ServerInstructions;

    public IReadOnlyList<PromptBuiltInOnlyDto> BuiltInOnly => BuiltInOnlyRows;

    public PromptFacts Facts(PromptKey key) => Facts_[IndexOf(key)];

    public string Text(PromptKey key) => Texts.GetOrAdd(key, Load);

    public string Hash(PromptKey key) => Hashes.GetOrAdd(key, k => PromptHash.Of(Text(k)));

    private static int IndexOf(PromptKey key)
        => Array.FindIndex(Facts_, facts => facts.Key == key) is var index and >= 0
            ? index
            : throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown prompt.");

    private static string Load(PromptKey key) => key switch
    {
        PromptKey.IssueProposal => PromptDefaults.IssueProposal,
        PromptKey.Work => PlanGuide.Read(PlanGuidePart.Work),
        PromptKey.Guide => PlanGuide.Read(PlanGuidePart.Guide),
        PromptKey.PlanReviewer => PlanGuide.Read(PlanGuidePart.PlanReviewer),
        PromptKey.Lead => PlanGuide.Read(PlanGuidePart.Lead),
        PromptKey.Brief => PlanGuide.Read(PlanGuidePart.Brief),
        PromptKey.Report => PlanGuide.Read(PlanGuidePart.Report),
        PromptKey.CodeReviewer => PlanGuide.Read(PlanGuidePart.CodeReviewer),
        PromptKey.Fix => PlanGuide.Read(PlanGuidePart.Fix),
        PromptKey.Retro => PlanGuide.Read(PlanGuidePart.Retro),
        PromptKey.RetroAnalysis => PlanGuide.Read(PlanGuidePart.RetroAnalysis),
        PromptKey.SprintDemo => ReadSprintDemo(),
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown prompt."),
    };

    private static string ReadSprintDemo()
    {
        using var stream = ApplicationMarker.Assembly.GetManifestResourceStream(SprintDemoResource)
            ?? throw new InvalidOperationException($"The embedded template {SprintDemoResource} is missing.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    // Every {{token}} of the template in first-appearance order: removing one drops an inserted part or a value, so the editor warns on each.
    private static IReadOnlyList<string> SprintDemoPlaceholders()
        => PlaceholderToken().Matches(ReadSprintDemo()).Select(match => match.Value).Distinct(StringComparer.Ordinal).ToList();

    [GeneratedRegex(@"\{\{[a-z_]+\}\}")]
    private static partial Regex PlaceholderToken();
}
