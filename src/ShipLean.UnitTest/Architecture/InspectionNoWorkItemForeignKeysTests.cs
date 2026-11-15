using System.Text.RegularExpressions;
using ShipLean.TestSupport;

namespace ShipLean.UnitTest.Architecture;

// Inspection runs name work items by key string, so the module can be lifted out later (SHIP-1463 D2): no inspection_* table
// may have a foreign key into work_item or into a table that itself hangs off a work item.
[TestFixture]
public class InspectionNoWorkItemForeignKeysTests
{
    private const string Prefix = "inspection_";

    private static readonly Regex Statement = new(
        @"\b(?:CREATE\s+TABLE(?:\s+IF\s+NOT\s+EXISTS)?|ALTER\s+TABLE(?:\s+ONLY)?)\s+(?:public\.)?([A-Za-z_][A-Za-z0-9_]*)(.*?);",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex References = new(@"\bREFERENCES\s+(?:public\.)?([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    [Test]
    public void Migrations_WhenScanned_ShouldNotLetAnInspectionTableReferenceAWorkItem()
    {
        var violations = Violations(MigrationFiles(), Prefix);

        violations.ShouldBeEmpty("Reference the item by key string (item_key), never by a foreign key.");
    }

    [Test]
    public void Violations_WhenScratchMigrationReferencesWorkItem_ShouldReportTheTable()
    {
        var path = Path.Combine(Path.GetTempPath(), $"inspection-fk-{Guid.NewGuid():N}.sql");
        File.WriteAllText(
            path,
            "CREATE TABLE work_item (id uuid PRIMARY KEY);\nCREATE TABLE comment (id uuid PRIMARY KEY, work_item_id uuid REFERENCES work_item (id));\n" +
            "CREATE TABLE inspection_ok (id uuid PRIMARY KEY, project_id uuid REFERENCES project (id));\n" +
            "CREATE TABLE inspection_bad (id uuid PRIMARY KEY, item_id uuid REFERENCES work_item (id));\n" +
            "ALTER TABLE inspection_ok ADD COLUMN c uuid REFERENCES comment (id);\n");
        try
        {
            Violations([path], Prefix).Select(v => v[(v.IndexOf(':') + 1)..]).ShouldBe(["inspection_bad->work_item", "inspection_ok->comment"]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // "path:table->target" for each inspection table that references work_item or a table that references it.
    private static List<string> Violations(IEnumerable<string> files, string prefix)
    {
        var statements = files.SelectMany(file => Statement.Matches(File.ReadAllText(file)).Select(m => (File: file, Table: m.Groups[1].Value.ToLowerInvariant(), m.Groups[2].Value))).ToList();
        var forbidden = new HashSet<string> { "work_item" };
        forbidden.UnionWith(statements
            .Where(s => !s.Table.StartsWith(prefix, StringComparison.Ordinal) && References.Matches(s.Value).Any(r => r.Groups[1].Value.Equals("work_item", StringComparison.OrdinalIgnoreCase)))
            .Select(s => s.Table));
        return [.. statements
            .Where(s => s.Table.StartsWith(prefix, StringComparison.Ordinal))
            .SelectMany(s => References.Matches(s.Value).Select(r => (s.File, s.Table, Target: r.Groups[1].Value.ToLowerInvariant())))
            .Where(r => forbidden.Contains(r.Target))
            .Select(r => $"{Path.GetRelativePath(RepoPaths.Root, r.File)}:{r.Table}->{r.Target}")
            .Order(StringComparer.Ordinal)];
    }

    private static IEnumerable<string> MigrationFiles()
        => new[] { "schema", "migrations" }.SelectMany(folder => Directory.EnumerateFiles(Path.Combine(RepoPaths.Root, "db", folder), "*.sql"));
}
