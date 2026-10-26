using ShipLean.TestSupport;

namespace ShipLean.UnitTest.Harness;

[TestFixture]
public class TestIdPinTests
{
    private string _root = null!;

    [SetUp]
    public void CreateRoot()
    {
        _root = Path.Combine(Path.GetTempPath(), "shiplean-testids-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void RemoveRoot() => Directory.Delete(_root, recursive: true);

    [Test]
    public void TestIds_ShouldMatchBetweenClientAndTests()
    {
        var problems = TestIdPin.DiscoverPairs(RepoPaths.Root).SelectMany(TestIdPin.Compare).ToList();

        problems.ShouldBeEmpty();
    }

    [Test]
    public void TestIds_WhenFilesMatch_ShouldPass()
    {
        WriteBoard("""
            export const boardTestIds = {

              cardTitle: 'board-card-title',
              lane: 'board-lane',
            } as const;
            """, ("CardTitle", "board-card-title"), ("Lane", "board-lane"));

        Problems().ShouldBeEmpty();
    }

    [Test]
    public void TestIds_WhenTsHasCommentLines_ShouldSkipThem()
    {
        WriteBoard("""
            export const boardTestIds = {
              // S0c block
              cardTitle: 'board-card-title',

              // F1 block
              lane: 'board-lane',
            } as const;
            """, ("CardTitle", "board-card-title"), ("Lane", "board-lane"));

        Problems().ShouldBeEmpty();
    }

    [Test]
    public void TestIds_WhenFilesDiffer_ShouldFail()
    {
        WriteBoard("""
            export const boardTestIds = {
              cardTitle: 'board-card-title',
            } as const;
            """, ("CardTitle", "board-card-heading"));

        Problems().ShouldHaveSingleItem().ShouldContain("CardTitle");
    }

    [Test]
    public void TestIds_WhenAKeyIsMissing_ShouldFail()
    {
        WriteBoard("""
            export const boardTestIds = {
              cardTitle: 'board-card-title',
              lane: 'board-lane',
            } as const;
            """, ("CardTitle", "board-card-title"));

        Problems().ShouldHaveSingleItem().ShouldContain("missing const Lane");
    }

    [Test]
    public void TestIds_WhenACsKeyHasNoTsKey_ShouldFail()
    {
        WriteBoard("""
            export const boardTestIds = {
              cardTitle: 'board-card-title',
            } as const;
            """, ("CardTitle", "board-card-title"), ("Lane", "board-lane"));

        Problems().ShouldHaveSingleItem().ShouldContain("const Lane has no key");
    }

    [Test]
    public void TestIds_WhenAKeyIsOnlyCasedDifferently_ShouldFail()
    {
        WriteBoard("""
            export const boardTestIds = {
              cardTitle: 'board-card-title',
            } as const;
            """, ("Cardtitle", "board-card-title"));

        Problems().Count.ShouldBe(2);
    }

    [Test]
    public void TestIds_WhenTheCsFileIsMissing_ShouldFail()
    {
        Write(Path.Combine("src", "ShipLean.Client", "src", "features", "board", "testIds.ts"), "export const boardTestIds = {} as const;");

        Problems().ShouldHaveSingleItem().ShouldContain("no C# class BoardTestIds");
    }

    [Test]
    public void TestIds_WhenTheTsFileIsMissing_ShouldFail()
    {
        Write(Path.Combine("src", "ShipLean.TestSupport", "TestIds", "BoardTestIds.cs"), "public static class BoardTestIds {}");

        Problems().ShouldHaveSingleItem().ShouldContain("no TypeScript file");
    }

    [Test]
    public void TestIds_WhenATestIdsFileIsMisplaced_ShouldFail()
    {
        Write(Path.Combine("src", "ShipLean.Client", "src", "features", "board", "cards", "testIds.ts"), "export const cardsTestIds = {} as const;");
        Write(Path.Combine("src", "ShipLean.Client", "src", "shell", "testIds.ts"), "export const shellTestIds = {} as const;");

        Problems().Count.ShouldBe(2);
        Problems().ShouldAllBe(p => p.Contains("belongs in"));
    }

    [Test]
    public void TestIds_WhenTheFixedPairsExist_ShouldBePaired()
    {
        Write(Path.Combine("src", "ShipLean.Client", "src", "testIds.ts"), "export const appTestIds = {\n  shell: 'app-shell',\n} as const;");
        Write(Path.Combine("src", "ShipLean.TestSupport", "TestIds", "AppTestIds.cs"), "public static class AppTestIds { public const string Shell = \"app-shell\"; }");
        Write(Path.Combine("src", "ShipLean.Client", "src", "components", "testIds.ts"), "export const componentTestIds = {\n  dialog: 'dialog',\n} as const;");
        Write(Path.Combine("src", "ShipLean.TestSupport", "TestIds", "ComponentTestIds.cs"), "public static class ComponentTestIds { public const string Dialog = \"dialog-x\"; }");

        Problems().ShouldHaveSingleItem().ShouldContain("ComponentTestIds.Dialog");
    }

    private List<string> Problems() => TestIdPin.DiscoverPairs(_root).SelectMany(TestIdPin.Compare).ToList();

    private void WriteBoard(string ts, params (string Name, string Value)[] consts)
    {
        Write(Path.Combine("src", "ShipLean.Client", "src", "features", "board", "testIds.ts"), ts);
        var lines = string.Concat(consts.Select(c => $"    public const string {c.Name} = \"{c.Value}\";\n"));
        Write(Path.Combine("src", "ShipLean.TestSupport", "TestIds", "BoardTestIds.cs"),
            "namespace ShipLean.TestSupport.TestIds;\npublic static class BoardTestIds\n{\n" + lines + "}\n");
    }

    private void Write(string relative, string content)
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
