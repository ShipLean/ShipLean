using System.Text.RegularExpressions;
using ShipLean.TestSupport;

namespace ShipLean.UnitTest.Architecture;

// App code never hard-deletes. "Delete" and "remove" set archived_at (or move to the Removed state) and keep the row;
// link rows are archived too and a re-add revives them. Only rows that are rebuilt from other rows may be deleted.
// Scans the C# under src/ (production projects) and the SQL in db/schema and db/migrations.
[TestFixture]
public class NoHardDeletesTests
{
    // Tables whose rows are rebuilt from other rows may be deleted from; name each with the reason.
    private static readonly Dictionary<string, string> Exempt = new(StringComparer.OrdinalIgnoreCase)
    {
        ["search_doc"] = "derived: rebuilt from items, comments, attachments, projects and sprints",
    };

    private static readonly string[] TestProjectMarkers = [".Tests", "ShipLean.UnitTest", "ShipLean.AcceptanceTests", "ShipLean.TestSupport"];

    private static readonly Regex Delete = new(@"\bDELETE\s+FROM\s+(?:ONLY\s+)?([A-Za-z_][A-Za-z0-9_.""]*)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Truncate = new(@"\bTRUNCATE\s+(?:TABLE\s+)?(?:ONLY\s+)?([A-Za-z_][A-Za-z0-9_.""]*)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    [Test]
    public void ProductionSql_WhenScanned_ShouldNotHardDeleteAnyEntityOrLinkTable()
    {
        var violations = Scan(ProductionFiles(), Exempt);

        violations.ShouldBeEmpty("Archive instead (archived_at / removed state); only the derived tables in Exempt may be deleted from.");
    }

    [Test]
    public void Scan_WhenSqlDeletesOrTruncates_ShouldReportEachTableExceptTheExemptOnes()
    {
        var path = Path.Combine(Path.GetTempPath(), $"no-hard-deletes-{Guid.NewGuid():N}.cs");
        File.WriteAllText(path, "a(\"delete  from work_item\");\nb(\"\"\"\n  DELETE FROM\n  ONLY sprint_item\"\"\");\nc(\"DELETE FROM derived_example\");\nd(\"TRUNCATE label\");\n");
        var exempt = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["derived_example"] = "derived: rebuilt from other rows" };
        try
        {
            Scan([path], exempt).Select(v => v[(v.IndexOf(':') + 1)..]).ShouldBe(["1:work_item", "3:sprint_item", "6:label"]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // "path:line:table", the line being where DELETE (or TRUNCATE) starts.
    private static List<string> Scan(IEnumerable<string> files, Dictionary<string, string> exempt)
    {
        var found = new List<string>();
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            foreach (var match in Delete.Matches(text).Concat(Truncate.Matches(text)))
            {
                var table = match.Groups[1].Value.Trim('"').ToLowerInvariant();
                if (table.StartsWith("public.", StringComparison.Ordinal))
                {
                    table = table["public.".Length..];
                }

                if (!exempt.ContainsKey(table))
                {
                    var line = text.AsSpan(0, match.Index).Count('\n') + 1;
                    found.Add($"{Path.GetRelativePath(RepoPaths.Root, file)}:{line}:{table}");
                }
            }
        }

        return found.Order(StringComparer.Ordinal).ToList();
    }

    private static IEnumerable<string> ProductionFiles()
    {
        var src = Path.Combine(RepoPaths.Root, "src");
        var sql = new[] { "schema", "migrations" }.SelectMany(folder =>
            Directory.EnumerateFiles(Path.Combine(RepoPaths.Root, "db", folder), "*.sql"));
        return sql.Concat(Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(f =>
            {
                var parts = Path.GetRelativePath(src, f).Split(Path.DirectorySeparatorChar);
                return !parts.Skip(1).Any(p => p is "bin" or "obj" or "node_modules")
                       && !TestProjectMarkers.Any(m => parts[0].EndsWith(m, StringComparison.Ordinal) || parts[0] == m);
            }));
    }
}
