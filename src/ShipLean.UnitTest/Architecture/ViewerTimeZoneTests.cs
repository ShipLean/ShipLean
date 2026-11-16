namespace ShipLean.UnitTest.Architecture;

// Every date and time a person sees is a DisplayDate label in the viewer's zone (IViewerZone.Zone). Production code
// outside DisplayDate may not touch zones (LocalTimeZone, TimeZoneInfo.Local, ToLocalTime, ConvertTime, zone lookups),
// call the old TimeProvider overloads (DisplayDate.Clock, DisplayDate.Weekday, DisplayDate.Relative(value, clock)) or
// format a date itself (a ToString or interpolation format with MMM, ddd or HH:mm that is neither ISO nor names UTC).
// The old overloads no longer exist, so a call to one fails the build; the scanner still flags them in case they return.
//
// Permanently allowed (see ViewerTimeZoneScanner): src/ShipLean.Core/DisplayDate.cs, everything. Sprint day labels
// ("d MMM", calendar days with no zone, D8) are DisplayDate.SprintDates too, so no other file is exempt.
[TestFixture]
public class ViewerTimeZoneTests
{
    [Test]
    public void ProductionCode_WhenScanned_ShouldHaveNoZoneCodeOutsideDisplayDate()
    {
        var found = ViewerTimeZoneScanner.ScanRepository()
            .Select(v => $"{v.Key.Path}\t{v.Key.Token}\t{v.Value}")
            .Order(StringComparer.Ordinal)
            .ToList();

        string.Join(Environment.NewLine, found).ShouldBeEmpty(
            "Build the label with a zone-taking DisplayDate overload and IViewerZone.Zone (time.GetUtcNow(), viewerZone.Zone); "
            + "machine and agent text uses ISO 8601 or DisplayDate.Format (explicit UTC).");
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
    [TestCase("using static System.TimeZoneInfo;\nvar a = ConvertTime(v, zone); var b = Local; var c = FindSystemTimeZoneById(id);",
        "FindSystemTimeZoneById|TimeZoneInfo.ConvertTime|TimeZoneInfo.Local|using static TimeZoneInfo")]
    [TestCase("global using static TimeZoneInfo;\nvar a = ConvertTimeToUtc(v); var k = DateTimeKind.Local; v.ToLocalTime();",
        "TimeZoneInfo.ConvertTime|ToLocalTime(|using static TimeZoneInfo")]
    [TestCase("using Z = System.TimeZoneInfo;\nvar a = Z.ConvertTime(v, zone); var b = Z . Local; Z.FindSystemTimeZoneById(id);",
        "FindSystemTimeZoneById|TimeZoneInfo.ConvertTime|TimeZoneInfo.Local|using alias TimeZoneInfo")]
    [TestCase("var a = ConvertTime(v, zone); var b = Local; var k = DateTimeKind.Local;", "")]
    [TestCase("var u = TimeZoneInfo.ConvertTime(v, TimeZoneInfo.Utc);", "")]
    [TestCase("var u = TimeZoneInfo.ConvertTime(v, TimeZoneInfo.Utc, TimeZoneInfo.Utc);", "")]
    [TestCase("var u = TimeZoneInfo.ConvertTime(v, zone, TimeZoneInfo.Utc);", "TimeZoneInfo.ConvertTime")]
    [TestCase("var u = TimeZoneInfo.ConvertTime(v, TimeZoneInfo.Utc.Next);", "TimeZoneInfo.ConvertTime")]
    [TestCase("using static System.TimeZoneInfo;\nvar u = ConvertTime(v, Utc);", "using static TimeZoneInfo")]
    [TestCase("using Z = System.TimeZoneInfo;\nvar u = Z.ConvertTime(v, Z.Utc);", "using alias TimeZoneInfo")]
    public void Scan_WhenSnippetScanned_ShouldFlagOnlyZoneCodeOldOverloadsAndAdHocFormats(string source, string expected)
    {
        string.Join("|", ViewerTimeZoneScanner.Scan(source)).ShouldBe(expected);
    }

    [TestCase("src/ShipLean.Core/SprintLabels.cs", "var s = d.ToString(\"d MMM\") + d.ToString(\"d MMM yyyy\");", "format \"d MMM yyyy\"|format \"d MMM\"")]
    [TestCase("src/ShipLean.Core/SprintLabels.cs", "var s = d.ToString(\"HH:mm\");", "format \"HH:mm\"")]
    [TestCase("src/ShipLean.Core/SprintLabels.cs", "var z = clock.LocalTimeZone;", "LocalTimeZone")]
    [TestCase("src/ShipLean.Core/Other.cs", "var s = d.ToString(\"d MMM\");", "format \"d MMM\"")]
    [TestCase("src/ShipLean.Core/DisplayDate.cs", "var s = d.ToString(\"HH:mm\"); var z = clock.LocalTimeZone;", "")]
    public void ScanFile_WhenPermanentException_ShouldAllowOnlyItsNamedCode(string path, string source, string expected)
    {
        string.Join("|", ViewerTimeZoneScanner.ScanFile(path, source)).ShouldBe(expected);
    }
}
