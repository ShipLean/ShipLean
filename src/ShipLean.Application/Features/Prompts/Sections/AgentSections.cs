using ShipLean.Contracts.Prompts;

namespace ShipLean.Application.Features.Prompts.Sections;

// C4: line-based agent sections. A tag line is a line whose trimmed text is exactly <id> or </id>; fenced code lines are never tags.
public static class AgentSections
{
    private const int MaxSuggestionDistance = 3;
    private static readonly string[] Ids = [PromptAgents.ClaudeCode, PromptAgents.OpenAiCli, PromptAgents.Cursor];

    private readonly record struct Line(string Text, bool IsTag, bool IsClose, string Name, bool IsKnown);

    // Total (D20): when Check finds an error, every agent gets the same untagged text and tagged regions are dropped.
    public static string Filter(string text, string agent)
    {
        var lines = Scan(text);
        var hasError = Problems(lines).Any(problem => problem.Kind != AgentSectionProblemKind.UnknownTag);
        var kept = new List<string>();
        string? section = null;
        foreach (var line in lines)
        {
            if (line.IsTag && line.IsKnown)
            {
                section = line.IsClose ? null : line.Name;
            }
            else if (section is null || (!hasError && section == agent))
            {
                kept.Add(line.Text);
            }
        }

        return string.Join('\n', kept);
    }

    public static IReadOnlyList<AgentSectionProblem> Check(string text) => Problems(Scan(text));

    private static List<AgentSectionProblem> Problems(List<Line> lines)
    {
        var problems = new List<AgentSectionProblem>();
        string? open = null;
        var openLine = 0;
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var number = index + 1;
            if (!line.IsTag)
            {
                continue;
            }

            if (!line.IsKnown)
            {
                var suggestion = Suggest(line.Name);
                if (suggestion is not null)
                {
                    problems.Add(new AgentSectionProblem(number, AgentSectionProblemKind.UnknownTag, line.Name, suggestion));
                }
            }
            else if (!line.IsClose && open is not null)
            {
                problems.Add(new AgentSectionProblem(number, AgentSectionProblemKind.Nested, line.Name, null));
            }
            else if (!line.IsClose)
            {
                open = line.Name;
                openLine = number;
            }
            else if (open is null || open != line.Name)
            {
                problems.Add(new AgentSectionProblem(number, AgentSectionProblemKind.StrayClose, line.Name, null));
            }
            else
            {
                open = null;
            }
        }

        if (open is not null)
        {
            problems.Add(new AgentSectionProblem(openLine, AgentSectionProblemKind.Unclosed, open, null));
        }

        return problems.OrderBy(problem => problem.Line).ToList();
    }

    private static List<Line> Scan(string text)
    {
        var result = new List<Line>();
        var fence = '\0';
        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            var trimmed = raw.Trim();
            var marker = FenceMarker(trimmed);
            if (fence == '\0' && marker != '\0')
            {
                fence = marker;
                result.Add(new Line(raw, false, false, string.Empty, false));
            }
            else if (fence != '\0')
            {
                fence = marker == fence ? '\0' : fence;
                result.Add(new Line(raw, false, false, string.Empty, false));
            }
            else
            {
                result.Add(ParseTag(raw, trimmed));
            }
        }

        return result;
    }

    private static char FenceMarker(string trimmed)
    {
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return '`';
        }

        return trimmed.StartsWith("~~~", StringComparison.Ordinal) ? '~' : '\0';
    }

    private static Line ParseTag(string raw, string trimmed)
    {
        if (trimmed.Length < 3 || trimmed[0] != '<' || trimmed[^1] != '>')
        {
            return new Line(raw, false, false, string.Empty, false);
        }

        var isClose = trimmed[1] == '/';
        var name = trimmed[(isClose ? 2 : 1)..^1];
        var wellFormed = name.Length > 0 && name[0] is >= 'a' and <= 'z' && name.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');
        return wellFormed
            ? new Line(raw, true, isClose, name, Ids.Contains(name))
            : new Line(raw, false, false, string.Empty, false);
    }

    private static string? Suggest(string name)
    {
        string? best = null;
        var bestDistance = MaxSuggestionDistance + 1;
        foreach (var id in Ids)
        {
            var distance = Distance(name, id);
            if (distance < bestDistance)
            {
                best = id;
                bestDistance = distance;
            }
        }

        return best;
    }

    private static int Distance(string a, string b)
    {
        var previous = Enumerable.Range(0, b.Length + 1).ToArray();
        for (var i = 1; i <= a.Length; i++)
        {
            var current = new int[b.Length + 1];
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }

            previous = current;
        }

        return previous[b.Length];
    }
}
