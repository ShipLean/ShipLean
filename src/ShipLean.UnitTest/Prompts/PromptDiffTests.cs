using System.Text;
using System.Text.Json;
using CsCheck;
using ShipLean.Application.Features.Prompts.Updates;
using ShipLean.TestSupport;

namespace ShipLean.UnitTest.Prompts;

[TestFixture]
public class PromptDiffTests
{
    private static readonly Gen<string> Texts =
        Gen.Select(Gen.String[Gen.Char["abc"], 0, 3].Array[0, 40], Gen.Bool)
            .Select(t => string.Join('\n', t.Item1) + (t.Item2 ? "\n" : ""));

    private static readonly Gen<(string From, string To)> Edited =
        Gen.Select(Texts, Gen.Int[0, 5].Array[0, 6], Gen.String[Gen.Char["abc"], 0, 3].Array[0, 6])
            .Select(t =>
            {
                var lines = t.Item1.Split('\n').ToList();
                for (var i = 0; i < t.Item2.Length && i < t.Item3.Length; i++)
                {
                    var at = t.Item2[i] % (lines.Count + 1);
                    if (t.Item2[i] % 2 == 0 && at < lines.Count)
                    {
                        lines.RemoveAt(at);
                    }
                    else
                    {
                        lines.Insert(at, t.Item3[i]);
                    }
                }

                return (t.Item1, string.Join('\n', lines));
            });

    public static IEnumerable<TestCaseData> FixtureCases()
    {
        var path = Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "DtoFixtures", "prompts", "prompt-patches.json");
        foreach (var item in JsonDocument.Parse(File.ReadAllText(path)).RootElement.GetProperty("cases").EnumerateArray())
        {
            yield return new TestCaseData(item.GetProperty("from").GetString(), item.GetProperty("to").GetString(), item.GetProperty("patch").GetString())
                .SetName($"Patch_WhenFixtureCase_ShouldEqualThePinnedPatch({item.GetProperty("name").GetString()})");
        }
    }

    [TestCaseSource(nameof(FixtureCases))]
    public void Patch_WhenFixtureCase_ShouldEqualThePinnedPatch(string from, string to, string patch)
    {
        PromptDiff.Patch(from, to).ShouldBe(patch);
    }

    [Test]
    public void Patch_WhenEqual_ShouldBeEmpty()
    {
        PromptDiff.Patch("a\nb\n", "a\nb\n").ShouldBe("");
    }

    [Test]
    public void Patch_WhenOneLineChanged_ShouldBeOneHunkWithContext()
    {
        var patch = PromptDiff.Patch("1\n2\n3\n4\n5\n6\n7\n8\n9\n", "1\n2\n3\n4\nfive\n6\n7\n8\n9\n");

        patch.ShouldBe("@@ -2,7 +2,7 @@\n 2\n 3\n 4\n-5\n+five\n 6\n 7\n 8\n");
    }

    [Test]
    public void Patch_WhenChangesFarApart_ShouldBeTwoHunks()
    {
        var from = string.Concat(Enumerable.Range(1, 30).Select(i => $"{i}\n"));
        var to = from.Replace("\n3\n", "\nthree\n", StringComparison.Ordinal).Replace("\n27\n", "\ntwenty-seven\n", StringComparison.Ordinal);

        PromptDiff.Patch(from, to).Split('\n').Count(l => l.StartsWith("@@", StringComparison.Ordinal)).ShouldBe(2);
    }

    [Test]
    public void Patch_WhenChangesSixLinesApart_ShouldBeOneHunk()
    {
        var from = string.Concat(Enumerable.Range(1, 20).Select(i => $"{i}\n"));
        var to = from.Replace("\n5\n", "\nfive\n", StringComparison.Ordinal).Replace("\n12\n", "\ntwelve\n", StringComparison.Ordinal);

        PromptDiff.Patch(from, to).Split('\n').Count(l => l.StartsWith("@@", StringComparison.Ordinal)).ShouldBe(1);
    }

    [Test]
    public void Patch_WhenNoTrailingNewline_ShouldSaySo()
    {
        PromptDiff.Patch("a\nb", "a\nb\n").ShouldBe("@@ -1,2 +1,2 @@\n a\n-b\n\\ No newline at end of file\n+b\n");
    }

    [Test]
    public void Patch_WhenCrLf_ShouldMatchLf()
    {
        PromptDiff.Patch("a\r\nb\r\n", "a\r\nc\r\n").ShouldBe(PromptDiff.Patch("a\nb\n", "a\nc\n"));
    }

    [Test]
    public void Patch_WhenFromIsEmpty_ShouldAddEveryLine()
    {
        PromptDiff.Patch("", "x\ny\n").ShouldBe("@@ -0,0 +1,2 @@\n+x\n+y\n");
    }

    [Test]
    public void Patch_WhenApplied_ShouldRebuildTheTarget()
    {
        Edited.Sample(pair => Apply(pair.From, PromptDiff.Patch(pair.From, pair.To)).ShouldBe(pair.To));
    }

    [Test]
    public void Patch_When64KBTexts_ShouldFinish()
    {
        var from = string.Concat(Enumerable.Range(0, 1500).Select(i => $"rule {i}: keep the agent's answer short and exact.\n"));
        var to = string.Concat(Enumerable.Range(0, 1500).Select(i => i % 7 == 0 ? $"changed rule {i}\n" : $"rule {i}: keep the agent's answer short and exact.\n"));
        var unrelated = string.Concat(Enumerable.Range(0, 1500).Select(i => $"other {i}: nothing in common with the first text.\n"));

        Apply(from, PromptDiff.Patch(from, to)).ShouldBe(to);
        Apply(from, PromptDiff.Patch(from, unrelated)).ShouldBe(unrelated);
    }

    [Test]
    public void Patch_When32000ShortLinesAllDiffer_ShouldFinish()
    {
        var from = string.Concat(Enumerable.Repeat("a\n", 32000));
        var to = string.Concat(Enumerable.Repeat("b\n", 32000));

        Apply(from, PromptDiff.Patch(from, to)).ShouldBe(to);
    }

    // Applies hunks to the old text: kept and '-' lines are checked against it, '+' lines are written.
    private static string Apply(string from, string patch)
    {
        var old = from.Length == 0 ? [] : from.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var oldLines = old.Length > 0 && old[^1] == "" ? old[..^1] : old;
                var output = new StringBuilder();
        var position = 0;
        var unterminated = false;
        var lines = patch.Length == 0 ? [] : patch.Split('\n')[..^1];
        var previous = ' ';
        foreach (var line in lines)
        {
            if (line.StartsWith("@@", StringComparison.Ordinal))
            {
                var oldStart = int.Parse(line.Split(' ')[1].TrimStart('-').Split(',')[0]);
                var count = line.Split(' ')[1].Contains(',') ? int.Parse(line.Split(' ')[1].Split(',')[1]) : 1;
                var target = count == 0 ? oldStart : oldStart - 1;
                while (position < target)
                {
                    output.Append(oldLines[position++]).Append('\n');
                }
            }
            else if (line.StartsWith('\\'))
            {
                // Which side the marker belongs to is the line before it; only a marker after '+' or ' ' describes the new text.
                unterminated = unterminated || previous != '-';
            }
            else if (line[0] == '+')
            {
                output.Append(line[1..]).Append('\n');
                unterminated = false;
            }
            else if (line[0] == '-')
            {
                position++;
            }
            else
            {
                output.Append(oldLines[position++]).Append('\n');
                unterminated = false;
            }

            previous = line.Length > 0 ? line[0] : ' ';
        }

        while (position < oldLines.Length)
        {
            output.Append(oldLines[position++]).Append('\n');
            unterminated = !from.EndsWith('\n');
        }

        var text = output.ToString();
        return unterminated && text.Length > 0 ? text[..^1] : text;
    }
}
