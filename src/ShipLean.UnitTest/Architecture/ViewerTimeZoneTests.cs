using ShipLean.TestSupport;

namespace ShipLean.UnitTest.Architecture;

// Every date and time a person sees is a DisplayDate label in the viewer's zone (IViewerZone.Zone). Production code
// outside DisplayDate may not touch zones (LocalTimeZone, TimeZoneInfo.Local, ToLocalTime, ConvertTime, zone lookups),
// call the old TimeProvider overloads (DisplayDate.Clock, DisplayDate.Weekday, DisplayDate.Relative(value, clock)) or
// format a date itself (a ToString or interpolation format with MMM, ddd or HH:mm that is neither ISO nor names UTC).
//
// Permanently allowed (see ViewerTimeZoneScanner): src/ShipLean.Core/DisplayDate.cs (everything) and
// src/ShipLean.Core/SprintLabels.cs (its "d MMM" formats only: sprint dates are calendar days, D8).
//
// Violations that exist today are listed in TimeZoneBaseline/<lane>.txt, one file per lane that fixes them, as
// "path<TAB>token<TAB>count" (no line numbers). A file may have at most its count; fixing one means lowering the count,
// and deleting the line at zero. Never add a line. TODO: remove the baseline mechanism once every lane file is empty.
[TestFixture]
public class ViewerTimeZoneTests
{
    private const string BaselineFolder = "TimeZoneBaseline";

    [Test]
    public void ProductionCode_WhenScanned_ShouldHaveNoZoneCodeOutsideDisplayDateOrTheBaseline()
    {
        var baseline = Baseline();
        var over = ViewerTimeZoneScanner.ScanRepository()
            .Where(v => v.Value > baseline.GetValueOrDefault(v.Key).Count)
            .Select(v => $"{v.Key.Path}\t{v.Key.Token}\t{v.Value}\t(the baseline allows {baseline.GetValueOrDefault(v.Key).Count})")
            .Order(StringComparer.Ordinal)
            .ToList();

        string.Join(Environment.NewLine, over).ShouldBeEmpty(
            "Build the label with a zone-taking DisplayDate overload and IViewerZone.Zone (time.GetUtcNow(), viewerZone.Zone); "
            + "machine text uses ISO 8601 or DisplayDate.Format. Never add a baseline line.");
    }

    [Test]
    public void Baseline_WhenScanned_ShouldListNoFixedViolation()
    {
        var actual = ViewerTimeZoneScanner.ScanRepository();
        var problems = Baseline()
            .Where(b => b.Value.Count > actual.GetValueOrDefault(b.Key))
            .Select(b => $"{b.Key.Path}\t{b.Key.Token}: the baseline says {b.Value.Count} but {actual.GetValueOrDefault(b.Key)} remain: "
                         + $"lower the count, or at zero remove it from {BaselineFolder}/{b.Value.Lane}.txt")
            .Concat(Duplicates())
            .Order(StringComparer.Ordinal)
            .ToList();

        string.Join(Environment.NewLine, problems).ShouldBeEmpty("A baseline entry above what remains hides a new violation.");
    }

    [TestCase("DisplayDate.Relative(a, now, zone);", "")]
    [TestCase("DisplayDate.Relative(a, clock);", ViewerTimeZoneScanner.OldRelative)]
    [TestCase("DisplayDate.Relative(F(a, b), clock);", ViewerTimeZoneScanner.OldRelative)]
    [TestCase("DisplayDate.Relative(Pick(rows, r => r.At), now, zone);", "")]
    [TestCase("DisplayDate.Relative(rows.Max(r => { var x = (r.At, 1); return x.Item1; }), clock);", ViewerTimeZoneScanner.OldRelative)]
    [TestCase("DisplayDate.Relative(a, \", \", clock);", "")]
    [TestCase("DisplayDate.Relative(\n    a,\n    clock)", ViewerTimeZoneScanner.OldRelative)]
    [TestCase("// DisplayDate.Relative(a, clock)\n/* clock.LocalTimeZone */", "")]
    [TestCase("var s = \"clock.LocalTimeZone \\\" DisplayDate.Clock(a, b)\";", "")]
    [TestCase("var s = @\"C:\\\" + DisplayDate.Clock(a, b);", "DisplayDate.Clock(")]
    [TestCase("var s = \"\"\"\n  DisplayDate.Relative(a, clock)\n  \"\"\";", "")]
    [TestCase("var s = $\"since {DisplayDate.Weekday(since, clock)} {DisplayDate.Clock(since, clock)}\";", "DisplayDate.Clock(|DisplayDate.Weekday(")]
    [TestCase("var s = $\"{(ok ? \"a\" : DisplayDate.Relative(a, clock))}\";", ViewerTimeZoneScanner.OldRelative)]
    [TestCase("var z = TimeZoneInfo.ConvertTime(v, clock.LocalTimeZone);", "LocalTimeZone|TimeZoneInfo.ConvertTime")]
    [TestCase("var z = TimeZoneInfo.Local; var u = TimeZoneInfo.Utc; v.ToLocalTime();", "TimeZoneInfo.Local|ToLocalTime(")]
    [TestCase("TimeZoneInfo.TryFindSystemTimeZoneById(id, out var z); TimeZoneInfo.GetSystemTimeZones();", "FindSystemTimeZoneById|GetSystemTimeZones")]
    [TestCase("void F(TimeZoneInfo zone) => G(zone);", "")]
    [TestCase("at.ToString(\"HH:mm\", CultureInfo.InvariantCulture);", "format \"HH:mm\"")]
    [TestCase("day.ToString( \"ddd d MMM\");", "format \"ddd d MMM\"")]
    [TestCase("at.ToString(\"yyyy-MM-dd'T'HH:mm:ss'Z'\");", "")]
    [TestCase("at.ToString(\"yyyy-MM-dd HH:mm 'UTC'\");", "")]
    [TestCase("MyToString(\"HH:mm\");", "")]
    [TestCase("var s = $\"{at:d MMM}\";", "format \"d MMM\"")]
    [TestCase("var s = $\"{at,8:HH:mm}\";", "format \"HH:mm\"")]
    [TestCase("var s = $\"until {at:yyyy-MM-dd HH:mm} UTC.\";", "")]
    [TestCase("var s = $\"merged {at:yyyy-MM-ddTHH:mm:ssZ}\";", "")]
    [TestCase("string[] formats = [\"yyyy-MM-dd HH:mm:ss zzz\"];", "")]
    public void Scan_WhenSnippetScanned_ShouldFlagOnlyZoneCodeOldOverloadsAndAdHocFormats(string source, string expected)
    {
        string.Join("|", ViewerTimeZoneScanner.Scan(source)).ShouldBe(expected);
    }

    // (path, token) -> (count, lane file), from every TimeZoneBaseline/*.txt.
    private static Dictionary<(string Path, string Token), (int Count, string Lane)> Baseline()
    {
        var baseline = new Dictionary<(string Path, string Token), (int Count, string Lane)>();
        foreach (var (key, count, lane) in BaselineLines())
        {
            var earlier = baseline.GetValueOrDefault(key).Count;
            baseline[key] = (earlier + count, lane);
        }

        return baseline;
    }

    private static IEnumerable<string> Duplicates() =>
        BaselineLines()
            .GroupBy(l => l.Key)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key.Path}\t{g.Key.Token}: listed more than once ({string.Join(", ", g.Select(l => l.Lane + ".txt"))}); keep one line");

    private static IEnumerable<((string Path, string Token) Key, int Count, string Lane)> BaselineLines()
    {
        var folder = Path.Combine(RepoPaths.Root, "src", "ShipLean.UnitTest", "Architecture", BaselineFolder);
        foreach (var file in Directory.EnumerateFiles(folder, "*.txt").Order(StringComparer.Ordinal))
        {
            var lane = Path.GetFileNameWithoutExtension(file);
            foreach (var line in File.ReadLines(file).Where(l => l.Length > 0 && !l.StartsWith('#')))
            {
                var parts = line.Split('\t');
                if (parts.Length != 3 || !int.TryParse(parts[2], out var count) || count < 1)
                {
                    throw new InvalidDataException($"{BaselineFolder}/{lane}.txt: '{line}' is not 'path<TAB>token<TAB>count' with a count of 1 or more.");
                }

                yield return ((parts[0], parts[1]), count, lane);
            }
        }
    }
}
