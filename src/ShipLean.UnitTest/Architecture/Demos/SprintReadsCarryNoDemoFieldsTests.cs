using System.Text.RegularExpressions;
using ShipLean.TestSupport;

namespace ShipLean.UnitTest.Architecture.Demos;

// K3 (lowercase demo_ only: the SHIPLEAN_DEMO_MAX_BYTES config key is upper case and allowed): the sprint's demo_ columns are read by exactly one thing, the demo reader under Features/Demos. A common sprint read that
// widens to the whole row (a column list that names demo_, SELECT * FROM sprint, to_jsonb of a sprint) would carry them everywhere.
[TestFixture]
public class SprintReadsCarryNoDemoFieldsTests
{
    private static readonly string[] TestProjectMarkers = [".Tests", "ShipLean.UnitTest", "ShipLean.AcceptanceTests", "ShipLean.TestSupport"];

    private static readonly string[] NamedFiles =
    [
        "ShipLean.Application/Features/Sprints/Crud/SprintRow.cs",
        "ShipLean.Application/Features/Retros/Sprint/SprintRow.cs",
        "ShipLean.Application/Features/Retros/Sprint/SprintRetroStore.cs",
    ];

    private static readonly Regex DemoField = new("demo_", RegexOptions.Compiled);
    private static readonly Regex SelectStar = new(@"SELECT\s+(?:DISTINCT\s+)?(?:\w+\.)?\*\s+FROM\s+sprint\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ToJsonb = new(@"to_jsonb\(\s*(?:sprint|s)\s*\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    [Test]
    public void Source_WhenScanned_ShouldKeepDemoFieldsInsideTheDemoFeature()
    {
        Violations(ProductionFiles()).ShouldBeEmpty("The demo_ columns belong to Features/Demos only.");
    }

    [TestCaseSource(nameof(NamedFiles))]
    public void NamedSprintReads_WhenRead_ShouldExistAndHoldNoDemoField(string file)
    {
        var text = File.ReadAllText(Path.Combine(SrcRoot(), file));

        DemoField.IsMatch(text).ShouldBeFalse();
        Violations([(file, text)]).ShouldBeEmpty();
    }

    [TestCaseSource(nameof(NamedFiles))]
    public void Violations_WhenACopyOfASprintReadNamesADemoField_ShouldReportIt(string file)
    {
        var copy = File.ReadAllText(Path.Combine(SrcRoot(), file)) + "\n// , demo_work_item_id";

        Violations([(file, copy)]).ShouldNotBeEmpty();
    }

    [Test]
    public void Violations_WhenAnyCodeOutsideTheDemoFeatureMentionsADemoField_ShouldReportIt()
    {
        Violations([("ShipLean.Application/Features/Sprints/SprintReads.cs", "\"SELECT demo_duration_seconds FROM sprint\"")]).ShouldNotBeEmpty();
        Violations([("ShipLean.Application/Features/Demos/App/Reads/SprintDemoRow.cs", "\"SELECT demo_duration_seconds FROM sprint\"")]).ShouldBeEmpty();
        Violations([("ShipLean.WebApi/Features/Demos/Web/Thing.cs", "demo_delivered_at")]).ShouldBeEmpty();
    }

    [Test]
    public void Violations_WhenAnUpperCaseConfigKeyHasDemo_ShouldBeEmptyButALowerCaseColumnShouldReport()
    {
        Violations([("ShipLean.Application/Storage/AttachmentLimits.cs", "\"SHIPLEAN_DEMO_MAX_BYTES\"")]).ShouldBeEmpty();
        Violations([("ShipLean.Application/Features/Sprints/SprintReads.cs", "\"SELECT demo_video_attachment_id FROM sprint\"")]).ShouldNotBeEmpty();
    }

    [TestCase("\"SELECT * FROM sprint WHERE id = @id\"")]
    [TestCase("\"SELECT s.* FROM sprint s\"")]
    [TestCase("\"select  DISTINCT\n *   from sprint\"")]
    [TestCase("\"SELECT to_jsonb(s) FROM sprint s\"")]
    [TestCase("\"SELECT to_jsonb(sprint) FROM sprint\"")]
    public void Violations_WhenASqlStringWidensTheSprintRow_ShouldReportIt(string sql)
    {
        Violations([("ShipLean.Application/Features/Sprints/Other.cs", sql)]).ShouldNotBeEmpty();
    }

    [Test]
    public void Violations_WhenSqlNamesItsColumnsOrReadsOtherTables_ShouldBeEmpty()
    {
        Violations([("ShipLean.Application/Features/Sprints/Other.cs", "\"SELECT id, name FROM sprint\" \"SELECT * FROM sprint_capacity\" \"SELECT to_jsonb(x) FROM thing x\"")]).ShouldBeEmpty();
    }

    // "path: what", for each file (path relative to src, '/' separated) that breaks a rule.
    private static List<string> Violations(IEnumerable<(string Path, string Text)> files)
    {
        var found = new List<string>();
        foreach (var (path, text) in files)
        {
            var inDemoFeature = path.Contains("/Features/Demos/", StringComparison.Ordinal);
            if (!inDemoFeature && DemoField.IsMatch(text))
            {
                found.Add($"{path}: names a demo_ field");
            }

            if (SelectStar.IsMatch(text) || ToJsonb.IsMatch(text))
            {
                found.Add($"{path}: reads the whole sprint row");
            }
        }

        return found.Order(StringComparer.Ordinal).ToList();
    }

    private static string SrcRoot() => Path.Combine(RepoPaths.Root, "src");

    private static IEnumerable<(string Path, string Text)> ProductionFiles()
    {
        var src = SrcRoot();
        foreach (var file in Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            var parts = Path.GetRelativePath(src, file).Split(Path.DirectorySeparatorChar);
            if (parts.Skip(1).Any(p => p is "bin" or "obj" or "node_modules")
                || TestProjectMarkers.Any(m => parts[0].EndsWith(m, StringComparison.Ordinal) || parts[0] == m))
            {
                continue;
            }

            yield return (string.Join('/', parts), File.ReadAllText(file));
        }
    }
}
