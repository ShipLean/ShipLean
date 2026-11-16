using System.Text;
using System.Text.RegularExpressions;
using ShipLean.TestSupport;

namespace ShipLean.UnitTest.Architecture;

// C2: every UPDATE that sets agent_session.state also sets end_reason (and end_detail) in the same statement, so a session never
// ends without saying why and a revived one never keeps a stale reason. Reads every whole C# string literal (regular, verbatim, raw,
// interpolated, multi-line) in the non-test projects under src/. For each UPDATE agent_session it looks only at the SET clause (up
// to the first WHERE, RETURNING, FROM or the next statement), so CASE WHEN state IN … and WHERE state = … are not assignments.
// An assignment is a SET item, literal or parameter (state = 'failed', state = @next); that item list must also assign end_reason.
// Known limit: SQL assembled from constants declared in other files (or other literals) is not seen, only each literal on its own.
[TestFixture]
public class AgentSessionTerminalWritersTests
{
    private static readonly string[] TestProjectMarkers = [".Tests", "ShipLean.UnitTest", "ShipLean.AcceptanceTests", "ShipLean.TestSupport"];

    private static readonly Regex Update = new(@"\bUPDATE\s+(?:ONLY\s+)?agent_session\b(?:\s+(?:AS\s+)?(?!SET\b)\w+)?\s+SET\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SetEnd = new(@"\b(?:WHERE|RETURNING|FROM|UPDATE|INSERT)\b|;", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex StateItem = new(@"(?:^|,)\s*(?:\w+\.)?state\s*=", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex EndReasonItem = new(@"(?:^|,)\s*(?:\w+\.)?end_reason\s*=", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // The writers C2 names, so a reader that stops finding literals fails here instead of passing on nothing.
    private static readonly string[] KnownWriters =
    [
        "src/ShipLean.Application/Features/AgentDispatch/AgentDeliverer.cs",
        "src/ShipLean.Application/Features/AgentDispatch/AgentReports.cs",
        "src/ShipLean.Application/Features/AgentDispatch/AgentStaleSweeper.cs",
    ];

    private const string MultiLineUpdate = """"
        class A
        {
            string Sql = """
                UPDATE agent_session
                SET state = @next,
                    ended_at = @ended,
                    end_reason = @reason, end_detail = @detail
                WHERE id = @id
                """;
        }
        """";

    private const string CaseWhenStateInSet = """"
        class B
        {
            string Sql = $@"UPDATE agent_session SET last_activity_at = CASE WHEN state IN {Live} THEN @now ELSE last_activity_at END,
                state = 'canceled', end_reason = 'canceled', end_detail = NULL
                WHERE state = ""working""";
        }
        """";

    private const string CteUpdate = """"
        class C
        {
            string Sql = $"""
                WITH ended AS (
                    UPDATE agent_session SET state = 'failed', ended_at = @now, end_reason = @reason, end_detail = @detail
                    WHERE state IN {AgentSessionStates.LiveSql}
                    RETURNING id),
                dropped AS (
                    UPDATE agent_delivery d SET status = 'failed', last_error = 'x' FROM ended WHERE d.session_id = ended.id)
                SELECT id FROM ended
                """;
        }
        """";

    private const string PlantedWriter = """
        class D
        {
            // "UPDATE agent_session SET state = 'x'" in a comment is not SQL.
            string Sql = "UPDATE agent_session SET state = @next WHERE id = @id AND state = 'working'";
            string Other = @"UPDATE agent_session SET last_activity_at = @now WHERE state = 'working'";
        }
        """;

    [Test]
    public void ProductionSql_WhenScanned_ShouldSetEndReasonWhereverItSetsState()
    {
        Violations(ProductionFiles()).ShouldBeEmpty("Every UPDATE agent_session that sets state must set end_reason and end_detail in the same SET (C2).");
    }

    [Test]
    public void ProductionSql_WhenScanned_ShouldFindTheStateWritersC2Names()
    {
        var writers = StateWriters(ProductionFiles()).Select(w => w.File).Distinct().Order(StringComparer.Ordinal);

        writers.ShouldBe(KnownWriters, ignoreOrder: true);
    }

    [Test]
    public void Violations_WhenAnUpdateSetsStateFromAParameterWithoutEndReason_ShouldReportIt()
    {
        Violations([("D.cs", PlantedWriter)]).ShouldBe(["D.cs:4"]);
    }

    [TestCase(nameof(MultiLineUpdate))]
    [TestCase(nameof(CaseWhenStateInSet))]
    [TestCase(nameof(CteUpdate))]
    public void Violations_WhenTheUpdateSetsEndReason_ShouldReportNothing(string fixture)
    {
        var source = Fixture(fixture);

        StateWriters([("F.cs", source)]).ShouldHaveSingleItem();
        Violations([("F.cs", source)]).ShouldBeEmpty();
    }

    [TestCase(nameof(MultiLineUpdate))]
    [TestCase(nameof(CaseWhenStateInSet))]
    [TestCase(nameof(CteUpdate))]
    public void Violations_WhenTheEndReasonIsRemovedFromAFixture_ShouldReportIt(string fixture)
    {
        var source = Regex.Replace(Fixture(fixture), @"end_reason\s*=\s*(?:@reason|'canceled'),", string.Empty);

        Violations([("F.cs", source)]).ShouldHaveSingleItem();
    }

    private static string Fixture(string name) => name switch
    {
        nameof(MultiLineUpdate) => MultiLineUpdate,
        nameof(CaseWhenStateInSet) => CaseWhenStateInSet,
        nameof(CteUpdate) => CteUpdate,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Not a fixture."),
    };

    // "file:line" for each state-setting UPDATE whose SET does not also set end_reason; the line is where the UPDATE starts.
    private static List<string> Violations(IEnumerable<(string File, string Text)> files)
        => StateWriters(files).Where(w => !w.SetsEndReason).Select(w => $"{w.File}:{w.Line}").Order(StringComparer.Ordinal).ToList();

    private static List<Writer> StateWriters(IEnumerable<(string File, string Text)> files)
    {
        var found = new List<Writer>();
        foreach (var (file, text) in files)
        {
            foreach (var literal in Literals(text))
            {
                foreach (Match update in Update.Matches(literal.Value))
                {
                    var rest = literal.Value[(update.Index + update.Length)..];
                    var end = SetEnd.Match(rest);
                    var set = end.Success ? rest[..end.Index] : rest;
                    if (StateItem.IsMatch(set))
                    {
                        var line = text.AsSpan(0, literal.Start).Count('\n') + literal.Value.AsSpan(0, update.Index).Count('\n') + 1;
                        found.Add(new Writer(file, line, EndReasonItem.IsMatch(set)));
                    }
                }
            }
        }

        return found;
    }

    // Every string literal in a C# file with its start offset: comments and char literals are skipped, and an interpolated
    // string's holes are kept as written (a hole's own nested strings are skipped over).
    private static List<Literal> Literals(string text)
    {
        var literals = new List<Literal>();
        var i = 0;
        while (i < text.Length)
        {
            if (text[i] == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                i = LineEnd(text, i);
            }
            else if (text[i] == '/' && i + 1 < text.Length && text[i + 1] == '*')
            {
                var close = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = close < 0 ? text.Length : close + 2;
            }
            else if (text[i] == '\'')
            {
                i = CharEnd(text, i);
            }
            else if (StringStart(text, i) is { } start)
            {
                var (value, next) = ReadString(text, i, start);
                literals.Add(new Literal(i, value));
                i = next;
            }
            else
            {
                i++;
            }
        }

        return literals;
    }

    private static int LineEnd(string text, int i)
    {
        var newline = text.IndexOf('\n', i);
        return newline < 0 ? text.Length : newline;
    }

    private static int CharEnd(string text, int i)
    {
        var j = i + 1;
        if (j < text.Length && text[j] == '\\')
        {
            j += 2;
        }
        else
        {
            j++;
        }

        while (j < text.Length && text[j] != '\'')
        {
            j++;
        }

        return j + 1;
    }

    // The prefix of a string literal at i ($ and @ in any order, then the opening quote), or null when none starts there.
    private static Prefix? StringStart(string text, int i)
    {
        var j = i;
        var dollars = 0;
        var verbatim = false;
        while (j < text.Length && text[j] is '$' or '@')
        {
            dollars += text[j] == '$' ? 1 : 0;
            verbatim |= text[j] == '@';
            j++;
        }

        if (j >= text.Length || text[j] != '"')
        {
            return null;
        }

        return new Prefix(j - i, dollars > 0, verbatim);
    }

    private static (string Value, int Next) ReadString(string text, int i, Prefix prefix)
    {
        var open = i + prefix.Length;
        var quotes = 0;
        while (open + quotes < text.Length && text[open + quotes] == '"')
        {
            quotes++;
        }

        if (quotes >= 3)
        {
            var delimiter = new string('"', quotes);
            var close = text.IndexOf(delimiter, open + quotes, StringComparison.Ordinal);
            return close < 0 ? (text[(open + quotes)..], text.Length) : (text[(open + quotes)..close], close + quotes);
        }

        if (quotes == 2 && !prefix.Verbatim)
        {
            return (string.Empty, open + 2);
        }

        var value = new StringBuilder();
        var j = open + 1;
        var depth = 0;
        while (j < text.Length)
        {
            var c = text[j];
            if (depth > 0 && (c == '"' || ((c is '$' or '@') && StringStart(text, j) is not null)))
            {
                var nested = StringStart(text, j)!;
                j = ReadString(text, j, nested).Next;
                continue;
            }

            if (depth == 0 && c == '"')
            {
                if (prefix.Verbatim && j + 1 < text.Length && text[j + 1] == '"')
                {
                    value.Append('"');
                    j += 2;
                    continue;
                }

                return (value.ToString(), j + 1);
            }

            if (!prefix.Verbatim && depth == 0 && c == '\\' && j + 1 < text.Length)
            {
                value.Append(text[j + 1]);
                j += 2;
                continue;
            }

            if (!prefix.Verbatim && depth == 0 && c == '\n')
            {
                return (value.ToString(), j);
            }

            if (prefix.Interpolated && (c is '{' or '}'))
            {
                if (depth == 0 && j + 1 < text.Length && text[j + 1] == c)
                {
                    value.Append(c);
                    j += 2;
                    continue;
                }

                depth += c == '{' ? 1 : -1;
            }

            value.Append(c);
            j++;
        }

        return (value.ToString(), j);
    }

    private static IEnumerable<(string File, string Text)> ProductionFiles()
    {
        var src = Path.Combine(RepoPaths.Root, "src");
        return Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(f =>
            {
                var parts = Path.GetRelativePath(src, f).Split(Path.DirectorySeparatorChar);
                return !parts.Skip(1).Any(p => p is "bin" or "obj" or "node_modules")
                       && !TestProjectMarkers.Any(m => parts[0].EndsWith(m, StringComparison.Ordinal) || parts[0] == m);
            })
            .Select(f => (Path.GetRelativePath(RepoPaths.Root, f).Replace(Path.DirectorySeparatorChar, '/'), File.ReadAllText(f)));
    }

    private sealed record Literal(int Start, string Value);

    private sealed record Prefix(int Length, bool Interpolated, bool Verbatim);

    private sealed record Writer(string File, int Line, bool SetsEndReason);
}
