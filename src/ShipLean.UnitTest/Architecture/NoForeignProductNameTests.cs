using ShipLean.TestSupport;

namespace ShipLean.UnitTest.Architecture;

// ShipLean is its own product: nothing in the repository, in a file's path or its text, may carry the name of the product
// it was started from. The name is assembled from parts so this file does not contain it.
[TestFixture]
public class NoForeignProductNameTests
{
    private static readonly string Needle = "Co" + "dex";

    // Build output, dependencies and the checkout's own metadata are not the repository's content.
    private static readonly string[] SkippedFolders =
        [".git", "node_modules", "bin", "obj", "wwwroot", "artifacts", "TestResults", "test-results", "playwright-report", ".pnpm-store", ".cache"];

    [Test]
    public void Repository_WhenScanned_ShouldNotMentionTheForeignProductName()
    {
        Mentions(RepoPaths.Root).ShouldBeEmpty("Describe what it is, not where it came from.");
    }

    [Test]
    public void Mentions_WhenAPathOrTheTextCarriesTheName_ShouldReportEachFileAnyCase()
    {
        var root = Directory.CreateTempSubdirectory("no-foreign-name-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(root, "clean.txt"), "nothing to see");
            File.WriteAllText(Path.Combine(root, "text.md"), $"ported from {Needle.ToUpperInvariant()}");
            File.WriteAllText(Path.Combine(root, $"{Needle.ToLowerInvariant()}-path.txt"), "clean text");
            Directory.CreateDirectory(Path.Combine(root, "node_modules"));
            File.WriteAllText(Path.Combine(root, "node_modules", "skipped.txt"), Needle);
            Directory.CreateDirectory(Path.Combine(root, "sub"));
            File.WriteAllBytes(Path.Combine(root, "sub", "binary.bin"), [0, 1, .. System.Text.Encoding.UTF8.GetBytes(Needle)]);

            Mentions(root).ShouldBe([$"{Needle.ToLowerInvariant()}-path.txt", "text.md"]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    // Repo-relative paths of the files whose path or text mentions the name.
    private static List<string> Mentions(string root)
        => Files(root)
            .Where(file => Path.GetRelativePath(root, file).Contains(Needle, StringComparison.OrdinalIgnoreCase)
                           || TextMentions(file))
            .Select(file => Path.GetRelativePath(root, file))
            .Order(StringComparer.Ordinal)
            .ToList();

    private static IEnumerable<string> Files(string folder)
    {
        foreach (var file in Directory.EnumerateFiles(folder))
        {
            yield return file;
        }
        foreach (var child in Directory.EnumerateDirectories(folder).Where(d => !SkippedFolders.Contains(Path.GetFileName(d))))
        {
            foreach (var file in Files(child))
            {
                yield return file;
            }
        }
    }

    // Binary files (a NUL byte early on: images, archives) are skipped; their paths are still checked.
    private static bool TextMentions(string file)
    {
        var bytes = File.ReadAllBytes(file);
        return !bytes.AsSpan(0, Math.Min(bytes.Length, 8192)).Contains((byte)0)
               && System.Text.Encoding.UTF8.GetString(bytes).Contains(Needle, StringComparison.OrdinalIgnoreCase);
    }
}
