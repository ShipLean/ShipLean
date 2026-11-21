using System.Text;

namespace ShipLean.Application.Features.Prompts.Updates;

public static class PromptDiff
{
    private const int Context = 3;
    private const int FunctionNameLimit = 80;

    /// <summary>Unified hunks (3 lines of context, no file header) in the shape `git diff -U3` writes; "" when the texts are equal.</summary>
    public static string Patch(string from, string to)
    {
        string[] a = [.. TextLines.Split(from)];
        string[] b = [.. TextLines.Split(to)];
        var edits = LineDiff.Edits(a, b);
        var output = new StringBuilder();

        var index = 0;
        while (true)
        {
            while (index < edits.Count && edits[index].Kind == ' ')
            {
                index++;
            }

            if (index == edits.Count)
            {
                return output.ToString();
            }

            // A hunk runs from the first change to the last change that is within 2 * Context kept lines of the previous one (git's rule),
            // so the context of two close changes is never printed twice.
            var first = index;
            var last = index;
            var scan = index;
            while (scan < edits.Count)
            {
                if (edits[scan].Kind != ' ')
                {
                    last = scan;
                    scan++;
                    continue;
                }

                var runEnd = scan;
                while (runEnd < edits.Count && edits[runEnd].Kind == ' ')
                {
                    runEnd++;
                }

                if (runEnd == edits.Count || runEnd - scan > 2 * Context)
                {
                    break;
                }

                scan = runEnd;
            }

            var start = Math.Max(0, first - Context);
            var end = Math.Min(edits.Count, last + 1 + Context);
            WriteHunk(output, a, b, edits, start, end);
            index = end;
        }
    }

    private static void WriteHunk(StringBuilder output, string[] a, string[] b, IReadOnlyList<LineEdit> edits, int start, int end)
    {
        var oldCount = 0;
        var newCount = 0;
        for (var i = start; i < end; i++)
        {
            if (edits[i].Kind != '+')
            {
                oldCount++;
            }

            if (edits[i].Kind != '-')
            {
                newCount++;
            }
        }

        // An empty side is numbered by the line before it, as git does ("-0,0" for an empty old text).
        var oldStart = oldCount == 0 ? edits[start].A : edits[start].A + 1;
        var newStart = newCount == 0 ? edits[start].B : edits[start].B + 1;
        output.Append("@@ -").Append(Range(oldStart, oldCount)).Append(" +").Append(Range(newStart, newCount)).Append(" @@");
        AppendFunctionName(output, a, edits[start].A);
        output.Append('\n');

        for (var i = start; i < end; i++)
        {
            var edit = edits[i];
            var line = edit.Kind == '+' ? b[edit.B] : a[edit.A];
            output.Append(edit.Kind).Append(line);
            if (!line.EndsWith('\n'))
            {
                output.Append("\n\\ No newline at end of file\n");
            }
        }
    }

    private static string Range(int start, int count) => count == 1 ? start.ToString() : $"{start},{count}";

    // git's default hunk heading: the closest earlier old line that starts with a letter, '_' or '$'.
    private static void AppendFunctionName(StringBuilder output, string[] a, int hunkOldStart)
    {
        for (var i = Math.Min(hunkOldStart, a.Length) - 1; i >= 0; i--)
        {
            var line = a[i];
            if (line.Length > 0 && (char.IsAsciiLetter(line[0]) || line[0] == '_' || line[0] == '$'))
            {
                var text = line.TrimEnd();
                output.Append(' ').Append(text.Length > FunctionNameLimit ? text[..FunctionNameLimit] : text);
                return;
            }
        }
    }
}
