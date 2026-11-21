using ShipLean.TestSupport;

namespace ShipLean.UnitTest.Architecture;

// Agents get a customizable prompt through IPromptResolver, and the built-in text is read only by BuiltInPrompts (and the guide reader it wraps).
// One row per way of reading a built-in: the text to look for, and the only files (relative to src/) allowed to contain it.
[TestFixture]
public class BuiltInPromptReadsTests
{
    private sealed record Rule(string Needle, string[] AllowedIn);

    private static readonly Rule[] Rules =
    [
        new("PlanGuide.Read(", ["ShipLean.WebApi/Features/Mcp/Planning/PlanGuide.cs", "ShipLean.WebApi/Features/Prompts/BuiltInPrompts.cs"]),
        new("sprint-demo/sprint-demo-prompt.md", ["ShipLean.WebApi/Features/Prompts/BuiltInPrompts.cs"]),
    ];

    private static readonly string[] Projects = ["ShipLean.WebApi", "ShipLean.Application", "ShipLean.Contracts", "ShipLean.Core"];

    [Test]
    public void Repository_WhenScanned_ShouldReadBuiltInsOnlyThroughBuiltInPrompts()
    {
        var src = Path.Combine(RepoPaths.Root, "src");
        var files = Projects.SelectMany(project => Directory.EnumerateFiles(Path.Combine(src, project), "*.cs", SearchOption.AllDirectories));

        Scan(files, src, Rules).ShouldBeEmpty("Ask IBuiltInPrompts, or IPromptResolver when an agent gets the text.");
    }

    [Test]
    public void Scan_WhenAFileReadsAGuidePart_ShouldReportIt()
    {
        var folder = Directory.CreateTempSubdirectory("built-in-reads-").FullName;
        var path = Path.Combine(folder, "Planted.cs");
        File.WriteAllText(path, "var text = PlanGuide.Read(PlanGuidePart.Guide);\n");
        try
        {
            Scan([path], folder, Rules).ShouldBe(["Planted.cs:1:PlanGuide.Read("]);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    // "path:line:needle" for each match in a file that is not allowed to hold it.
    private static List<string> Scan(IEnumerable<string> files, string root, Rule[] rules)
    {
        var found = new List<string>();
        foreach (var file in files)
        {
            var relative = Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
            var lines = File.ReadAllLines(file);
            foreach (var rule in rules.Where(r => !r.AllowedIn.Contains(relative, StringComparer.Ordinal)))
            {
                for (var index = 0; index < lines.Length; index++)
                {
                    if (lines[index].Contains(rule.Needle, StringComparison.Ordinal))
                    {
                        found.Add($"{relative}:{index + 1}:{rule.Needle}");
                    }
                }
            }
        }

        return found.Order(StringComparer.Ordinal).ToList();
    }
}
