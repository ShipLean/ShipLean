using System.Text;
using System.Text.RegularExpressions;
using ShipLean.Application.Features.Mcp.Planning.Waves;
using ShipLean.Application.Markdown;
using ShipLean.Application.WorkItems;
using ShipLean.WebApi.Features.Mcp.Tools.Planning;

namespace ShipLean.WebApi.Features.Mcp.Planning;

// The shape a plan must have for agents to run it, plus what apply would refuse, so a preview never passes a plan that
// capture_plan cannot create. Sizes are UTF-8 bytes.
public sealed partial class PlanShapeRules : IPlanShapeRules
{
    public const int MaxItems = 60;
    public const int MaxTaskBriefBytes = 10 * 1024;
    public const int MaxCommonBriefBytes = 64 * 1024;
    public const int MaxPlanMarkdownBytes = 200 * 1024;
    public const int ContextErrorOverK = 150;
    public const int ContextWarningOverK = 100;
    // The store refuses points outside 0 to 9999.99 (WorkItemChecks).
    public const decimal MaxPoints = 9999.99m;
    public const int MaxTitleLength = 255;
    public const int MaxTagValueLength = 64;
    public const int MaxBranchLength = 200;

    public IReadOnlyList<PlanFinding> Check(CapturePlanInput plan)
    {
        var findings = new List<PlanFinding>();
        CheckFeature(plan, findings);
        if (string.IsNullOrWhiteSpace(plan.CommonBrief))
        {
            findings.Add(Error("common_brief", "The common brief is required; every agent reads it."));
        }
        else
        {
            CheckSize("common_brief", plan.CommonBrief, MaxCommonBriefBytes, "The common brief", findings);
        }
        if (string.IsNullOrWhiteSpace(plan.SharedBranch))
        {
            findings.Add(Error("shared_branch", "The shared branch is required; every brief names it."));
        }
        else if (plan.SharedBranch.Any(char.IsWhiteSpace))
        {
            findings.Add(Error("shared_branch", "A branch name can't contain whitespace."));
        }
        else if (!IsSafeBranch(plan.SharedBranch))
        {
            findings.Add(Error("shared_branch", $"\"{Shorten(plan.SharedBranch)}\" is not a plain git branch name: use letters, digits, '.', '_', '-' and '/' only, at most {MaxBranchLength} characters, not starting with '-', '.' or '/', with no '..' or '//', and not ending with '/', '.' or '.lock'."));
        }
        CheckSize("plan_markdown", plan.PlanMarkdown, MaxPlanMarkdownBytes, "The plan document", findings);

        if (plan.Stories is null or { Count: 0 })
        {
            findings.Add(Error("stories", "A plan needs at least one story."));
        }
        var stories = plan.Stories ?? [];
        var items = stories.Count + stories.Sum(story => story?.Tasks?.Count ?? 0) + (string.IsNullOrWhiteSpace(plan.Feature?.Key) ? 1 : 0);
        if (items > MaxItems)
        {
            findings.Add(Error("stories", $"The plan creates {items} items; at most {MaxItems}. Split it into more than one feature or cut tasks."));
        }

        for (var s = 0; s < stories.Count; s++)
        {
            if (stories[s] is null)
            {
                findings.Add(Error($"stories[{s}]", "A story can't be null."));
                continue;
            }
            CheckStory(stories[s], $"stories[{s}]", findings);
        }
        CheckWaves(stories, findings);
        CheckLocalState("common_brief", plan.CommonBrief, findings);
        CheckLocalState("plan_markdown", plan.PlanMarkdown, findings);
        for (var s = 0; s < stories.Count; s++)
        {
            if (stories[s]?.Tasks is null)
            {
                continue;
            }
            for (var t = 0; t < stories[s].Tasks!.Count; t++)
            {
                CheckLocalState($"stories[{s}].tasks[{t}].brief", stories[s].Tasks![t]?.Brief, findings);
            }
        }
        return findings;
    }

    private static void CheckLocalState(string field, string? text, List<PlanFinding> findings)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        if (text.Contains("~/", StringComparison.Ordinal) || text.Contains("/home/", StringComparison.Ordinal)
            || text.Contains("$HOME", StringComparison.Ordinal))
        {
            findings.Add(new PlanFinding(
                PlanFindingSeverity.Warning, field,
                "The text points at a path on the person's machine. Plan state belongs in ShipLean (comments, descriptions, attach_markdown), not local files."));
        }
    }

    private static void CheckFeature(CapturePlanInput plan, List<PlanFinding> findings)
    {
        var feature = plan.Feature;
        if (string.IsNullOrWhiteSpace(feature?.Key) && string.IsNullOrWhiteSpace(feature?.Title))
        {
            findings.Add(Error("feature.title", "Give a new feature a title, or the key of an existing feature."));
        }
        CheckTitle("feature.title", feature?.Title, findings);
        CheckDescription("feature.description", feature?.Description, findings);
        if (feature?.Goal is { Length: > FeatureFieldRules.GoalMaxLength })
        {
            findings.Add(Error("feature.goal", $"The goal is {feature.Goal.Length:N0} characters; at most {FeatureFieldRules.GoalMaxLength:N0}."));
        }
    }

    private static void CheckStory(CapturePlanStory story, string path, List<PlanFinding> findings)
    {
        CheckDescription($"{path}.description", story.Description, findings);
        if (string.IsNullOrWhiteSpace(story.Title))
        {
            findings.Add(Error($"{path}.title", "A story needs a title."));
        }
        CheckTitle($"{path}.title", story.Title, findings);
        CheckTagValue($"{path}.lane", story.Lane, "A lane", findings);
        if (string.IsNullOrWhiteSpace(story.Wave))
        {
            findings.Add(Error($"{path}.wave", "A story needs a wave, such as 0.1, 1 or 2."));
        }
        else if (!WaveLabel.TryParse(story.Wave, out _))
        {
            findings.Add(Error($"{path}.wave", $"\"{story.Wave}\" is not a wave label; use whole numbers joined by dots, such as 0.1, 1 or 2."));
        }

        if (story.Tasks is null)
        {
            findings.Add(Error($"{path}.tasks", "A story needs its tasks."));
            return;
        }
        if (story.Tasks.Count == 0)
        {
            findings.Add(Error($"{path}.tasks", "A story needs at least one task."));
        }
        for (var t = 0; t < story.Tasks.Count; t++)
        {
            if (story.Tasks[t] is null)
            {
                findings.Add(Error($"{path}.tasks[{t}]", "A task can't be null."));
                continue;
            }
            CheckTask(story.Tasks[t], $"{path}.tasks[{t}]", findings);
        }
    }

    private static void CheckTask(CapturePlanTask task, string path, List<PlanFinding> findings)
    {
        if (string.IsNullOrWhiteSpace(task.Title))
        {
            findings.Add(Error($"{path}.title", "A task needs a title."));
        }
        CheckTitle($"{path}.title", task.Title, findings);
        if (string.IsNullOrWhiteSpace(task.Brief))
        {
            findings.Add(Error($"{path}.brief", "A task needs a brief."));
        }
        else
        {
            CheckSize($"{path}.brief", task.Brief, MaxTaskBriefBytes, "The brief", findings);
        }
        if (string.IsNullOrWhiteSpace(task.Model))
        {
            findings.Add(Error($"{path}.model", "A task needs a model role."));
        }
        CheckTagValue($"{path}.model", task.Model, "A model role", findings);
        if (task.Points is < 0 or > MaxPoints)
        {
            findings.Add(Error($"{path}.points", $"Points run from 0 to {MaxPoints:N2}."));
        }
        switch (task.ContextK)
        {
            case null:
                findings.Add(Error($"{path}.context_k", "A task needs the context it should fit in, in thousands of tokens."));
                break;
            case < 1:
                findings.Add(Error($"{path}.context_k", "A task's context is at least 1k."));
                break;
            case > ContextErrorOverK:
                findings.Add(Error($"{path}.context_k", $"{task.ContextK}k is over {ContextErrorOverK}k; split the task."));
                break;
            case > ContextWarningOverK:
                findings.Add(new PlanFinding(PlanFindingSeverity.Warning, $"{path}.context_k", $"{task.ContextK}k is over {ContextWarningOverK}k; consider splitting the task."));
                break;
        }
    }

    // A wave without lanes is one story; lane names are unique within a wave.
    private static void CheckWaves(IReadOnlyList<CapturePlanStory> stories, List<PlanFinding> findings)
    {
        var waves = new Dictionary<string, List<int>>();
        for (var s = 0; s < stories.Count; s++)
        {
            if (stories[s] is not null && WaveLabel.TryParse(stories[s].Wave, out var label))
            {
                if (!waves.TryGetValue(label.Text, out var indexes))
                {
                    waves[label.Text] = indexes = [];
                }
                indexes.Add(s);
            }
        }

        foreach (var (wave, indexes) in waves)
        {
            if (indexes.Count > 1 && indexes.Any(s => string.IsNullOrWhiteSpace(stories[s].Lane)))
            {
                // All lane-less: the first may stay as it is. Mixed: the lane-less ones are the ones to fix.
                var laneless = indexes.Where(s => string.IsNullOrWhiteSpace(stories[s].Lane)).ToList();
                foreach (var s in laneless.Count == indexes.Count ? laneless.Skip(1) : laneless)
                {
                    findings.Add(Error($"stories[{s}].lane", $"Wave {wave} has more than one story, so every story in it needs a lane."));
                }
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var s in indexes)
            {
                var lane = stories[s].Lane?.Trim();
                if (!string.IsNullOrEmpty(lane) && !seen.Add(lane))
                {
                    findings.Add(Error($"stories[{s}].lane", $"Lane \"{lane}\" is used twice in wave {wave}."));
                }
            }
        }
    }

    // A description over the renderer's limit would be saved and then fail to render.
    private static void CheckDescription(string field, string? text, List<PlanFinding> findings)
    {
        if (text is { Length: > MarkdownRenderer.MaxLength })
        {
            findings.Add(Error(field, $"The description is {text.Length:N0} characters; at most {MarkdownRenderer.MaxLength:N0}."));
        }
    }

    private static void CheckSize(string field, string? text, int maxBytes, string what, List<PlanFinding> findings)
    {
        if (text is not null && Encoding.UTF8.GetByteCount(text) > maxBytes)
        {
            findings.Add(Error(field, $"{what} is over {maxBytes / 1024} KB."));
        }
    }

    // Titles land in brief headings and table cells: one line, and short.
    private static void CheckTitle(string field, string? title, List<PlanFinding> findings)
    {
        if (title is null)
        {
            return;
        }
        if (title.Any(char.IsControl))
        {
            findings.Add(Error(field, "A title is one line: no line breaks, tabs or other control characters."));
        }
        else if (title.Trim().Length > MaxTitleLength)
        {
            findings.Add(Error(field, $"The title is {title.Trim().Length:N0} characters; at most {MaxTitleLength}."));
        }
    }

    // Lane and model become tags (lane:<x>, model:<x>) and land in briefs, so they are short plain words. Checked trimmed, as stored.
    private static void CheckTagValue(string field, string? value, string what, List<PlanFinding> findings)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }
        var trimmed = value.Trim();
        if (trimmed.Length > MaxTagValueLength || !TagValue().IsMatch(trimmed))
        {
            findings.Add(Error(field, $"{what} is a short word: letters, digits, '.', '_' and '-' only, at most {MaxTagValueLength} characters."));
        }
    }

    // A branch name every brief puts in git commands: plain characters only, and git's own ref rules for them.
    private static bool IsSafeBranch(string branch)
        => branch.Length <= MaxBranchLength
            && BranchCharacters().IsMatch(branch)
            && branch[0] is not ('-' or '.' or '/')
            && !branch.Contains("..", StringComparison.Ordinal)
            && !branch.Contains("//", StringComparison.Ordinal)
            && !branch.Contains("/.", StringComparison.Ordinal)
            && !branch.EndsWith('/') && !branch.EndsWith('.') && !branch.EndsWith(".lock", StringComparison.Ordinal);

    private static string Shorten(string text) => text.Length <= 40 ? text : text[..40] + "…";

    [GeneratedRegex("^[A-Za-z0-9._-]+$")]
    private static partial Regex TagValue();

    [GeneratedRegex("^[A-Za-z0-9._/-]+$")]
    private static partial Regex BranchCharacters();

    private static PlanFinding Error(string field, string message) => new(PlanFindingSeverity.Error, field, message);
}
