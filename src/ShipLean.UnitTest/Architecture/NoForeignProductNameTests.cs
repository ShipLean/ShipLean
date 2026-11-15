using ShipLean.TestSupport;

namespace ShipLean.UnitTest.Architecture;

// ShipLean is its own product: nothing in the repository, in a file's path or its text, may carry the name of the product
// it was started from. The name is assembled from parts so this file does not contain it.
[TestFixture]
public class NoForeignProductNameTests
{
    private static readonly string Needle = "Co" + "dex";

    // OpenAI's coding agent is a supported agent and is named exactly these ways (agent ids, labels, its event prefix, its config
    // folder and its service name); anything else that carries the name still fails. Matched case-sensitively, then removed.
    private static readonly string[] AllowedStrings = ["Codex CLI", "codex-cli", "codex.", ".codex/", "codex_cli_rs"];

    // Build output, dependencies and the checkout's own metadata are not the repository's content.
    private static readonly string[] SkippedFolders =
        [".git", "node_modules", "bin", "obj", "wwwroot", "artifacts", "TestResults", "test-results", "playwright-report", ".pnpm-store", ".cache"];

    [Test]
    public void Repository_WhenScanned_ShouldNotMentionTheForeignProductName()
    {
        Mentions(RepoPaths.Root).ShouldBeEmpty("Describe what it is, not where it came from.");
    }

    [Test]
    public void Mentions_WhenTheNameAppearsAlone_ShouldReportTheFile()
    {
        var root = Directory.CreateTempSubdirectory("no-foreign-name-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(root, "alone.md"), $"the {Needle} app");
            File.WriteAllText(Path.Combine(root, "near.md"), $"{Needle} CLI is allowed but not {Needle} Suite");

            Mentions(root).ShouldBe(["alone.md", "near.md"]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestCaseSource(nameof(AllowedStrings))]
    public void Mentions_WhenTheTextIsAnAllowedAgentName_ShouldNotReportTheFile(string allowed)
    {
        var root = Directory.CreateTempSubdirectory("no-foreign-name-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(root, "allowed.md"), $"uses {allowed} here");

            Mentions(root).ShouldBeEmpty();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void Mentions_WhenAPathHoldsAnAllowedName_ShouldNotReportTheFile()
    {
        var root = Directory.CreateTempSubdirectory("no-foreign-name-").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(root, ".codex"));
            File.WriteAllText(Path.Combine(root, ".codex", "config.toml"), "clean");
            File.WriteAllText(Path.Combine(root, "codex-cli-synthetic.json"), "clean");

            Mentions(root).ShouldBeEmpty();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
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
            .Where(file => Carries(Path.GetRelativePath(root, file).Replace('\\', '/')) || TextMentions(file))
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
               && Carries(System.Text.Encoding.UTF8.GetString(bytes));
    }

    private static bool Carries(string text)
        => AllowedStrings.Aggregate(text, (rest, allowed) => rest.Replace(allowed, string.Empty, StringComparison.Ordinal))
            .Contains(Needle, StringComparison.OrdinalIgnoreCase);
}
