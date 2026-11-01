using ShipLean.LocalHost;

namespace ShipLean.UnitTest.LocalHost;

[TestFixture]
public class LocalHostOptionsTests
{
    [Test]
    public void Parse_WhenNoArguments_ShouldUseDefaults()
    {
        var result = LocalHostOptionsParser.Parse([]);

        result.Error.ShouldBeNull();
        result.Options.ShouldBe(new LocalHostOptions { Port = 5113, Data = LocalHostData.Fresh, Database = "shiplean_local", Redis = false });
    }

    [Test]
    public void Parse_WhenAllOptionsGiven_ShouldReadThem()
    {
        var result = LocalHostOptionsParser.Parse(["--port", "6000", "--data", "keep", "--database", "shiplean_agent_1", "--redis"]);

        result.Error.ShouldBeNull();
        result.Options.ShouldBe(new LocalHostOptions { Port = 6000, Data = LocalHostData.Keep, Database = "shiplean_agent_1", Redis = true });
    }

    [Test]
    public void Parse_WhenPortIsZero_ShouldAcceptIt()
    {
        var result = LocalHostOptionsParser.Parse(["--port", "0"]);

        result.Options!.Port.ShouldBe(0);
    }

    [Test]
    public void Parse_WhenPortIsNotANumber_ShouldFail()
    {
        var result = LocalHostOptionsParser.Parse(["--port", "abc"]);

        result.Options.ShouldBeNull();
        result.Error!.ShouldContain("--port");
    }

    [TestCase("65536")]
    [TestCase("-1")]
    public void Parse_WhenPortIsOutOfRange_ShouldFail(string port)
    {
        var result = LocalHostOptionsParser.Parse(["--port", port]);

        result.Options.ShouldBeNull();
        result.Error!.ShouldContain("--port");
    }

    [TestCase("--port")]
    [TestCase("--data")]
    [TestCase("--database")]
    public void Parse_WhenValueIsMissing_ShouldFail(string flag)
    {
        var result = LocalHostOptionsParser.Parse([flag]);

        result.Options.ShouldBeNull();
        result.Error!.ShouldContain(flag);
    }

    [TestCase("sometimes")]
    [TestCase("3")]
    public void Parse_WhenDataIsUnknown_ShouldFail(string data)
    {
        var result = LocalHostOptionsParser.Parse(["--data", data]);

        result.Options.ShouldBeNull();
        result.Error!.ShouldContain("--data");
    }

    [Test]
    public void Parse_WhenFlagIsUnknown_ShouldFail()
    {
        var result = LocalHostOptionsParser.Parse(["--bogus"]);

        result.Options.ShouldBeNull();
        result.Error!.ShouldContain("--bogus");
    }

    [TestCase("local")]
    [TestCase("shiplean_Local")]
    [TestCase("shiplean_f_1234567890_x")]
    public void Parse_WhenDatabaseNameIsInvalid_ShouldFail(string name)
    {
        var result = LocalHostOptionsParser.Parse(["--database", name]);

        result.Options.ShouldBeNull();
        result.Error!.ShouldContain("--database");
    }
}
