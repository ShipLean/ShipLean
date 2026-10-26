using System.Text;
using ShipLean.Application.Features.Mcp.Planning.Markers;
using ShipLean.Application.Features.Mcp.Planning.Tree;
using ShipLean.Application.Features.Mcp.Planning.Waves;

namespace ShipLean.WebApi.Features.Mcp.Planning.Briefs;

// The Markdown of a brief for each kind of item: the guide's templates filled for a task and a story, a table of waves for a feature.
public static class BriefMarkdown
{
    public const string NoCommonBrief = "_This plan has no common brief attached._";
    private const string NoContracts = "_The feature description has no Contracts section._";
    private const string NoBranch = "_(no shared branch set)_";
    private const string Dash = "-";

    // commonBrief is the Feature's live common-brief.md, null when it has none.
    public static string ForTask(PlanTree tree, PlanTask task, string? commonBrief, string reportTemplate)
        => PlanTemplate.Fill(PlanGuide.Read(PlanGuidePart.Brief), new Dictionary<string, string>
        {
            [PlanPlaceholders.CommonBrief] = commonBrief ?? NoCommonBrief,
            [PlanPlaceholders.TaskKey] = task.Key,
            [PlanPlaceholders.TaskTitle] = Line(task.Title),
            [PlanPlaceholders.TaskBrief] = task.Description,
            [PlanPlaceholders.SharedBranch] = tree.Feature.SharedBranch ?? NoBranch,
            [PlanPlaceholders.ReportTemplate] = reportTemplate,
        });

    public static string ForStory(PlanTree tree, PlanStory story)
        => PlanTemplate.Fill(PlanGuide.Read(PlanGuidePart.Lead), new Dictionary<string, string>
        {
            [PlanPlaceholders.FeatureKey] = tree.Feature.Key,
            [PlanPlaceholders.StoryKey] = story.Key,
            [PlanPlaceholders.Wave] = story.Wave.Text,
            [PlanPlaceholders.Lane] = story.Lane ?? Dash,
            [PlanPlaceholders.Tasks] = TaskTable(story),
            [PlanPlaceholders.Contracts] = MarkdownSections.Extract(tree.Feature.Description, PlanHeadings.Contracts) ?? NoContracts,
            [PlanPlaceholders.SharedBranch] = tree.Feature.SharedBranch ?? NoBranch,
        });

    // The feature's key, title and goal (when set), then each wave with a table of its stories.
    public static string ForFeature(PlanTree tree, IReadOnlyList<PlanWave> waves, WaveLabel? startable)
    {
        var feature = tree.Feature;
        var text = new StringBuilder().Append("# ").Append(feature.Key).Append(": ").AppendLine(Line(feature.Title));
        if (feature.Goal.Length > 0)
        {
            text.AppendLine().Append("Goal: ").AppendLine(feature.Goal);
        }
        text.AppendLine().AppendLine(startable is null ? "Every wave is done." : $"Startable wave: {startable.Text}");

        foreach (var wave in waves)
        {
            text.AppendLine().Append("## Wave ").Append(wave.Wave.Text).AppendLine(wave.Done ? " (done)" : string.Empty).AppendLine();
            text.AppendLine("| Story | Title | Lane | State |").AppendLine("|---|---|---|---|");
            foreach (var story in wave.Stories)
            {
                text.Append("| ").Append(story.Key).Append(" | ").Append(Cell(story.Title)).Append(" | ").Append(story.Lane ?? Dash)
                    .Append(" | ").Append(Cell(story.StateName)).AppendLine(" |");
            }
        }
        return text.ToString();
    }

    private static string TaskTable(PlanStory story)
    {
        var text = new StringBuilder().AppendLine("| Task | Title | Model | Context | State |").AppendLine("|---|---|---|---|---|");
        foreach (var task in story.Tasks)
        {
            text.Append("| ").Append(task.Key).Append(" | ").Append(Cell(task.Title)).Append(" | ").Append(task.Tags.Model ?? Dash)
                .Append(" | ").Append(task.Tags.ContextK is { } k ? $"{k}k" : Dash).Append(" | ").Append(Cell(task.StateName)).AppendLine(" |");
        }
        return text.ToString().TrimEnd();
    }

    // A title as one line, for a heading: a line break in it would start a section of its own.
    private static string Line(string text) => text.ReplaceLineEndings(" ");

    private static string Cell(string text) => text.Replace("|", "\\|", StringComparison.Ordinal).ReplaceLineEndings(" ");
}
