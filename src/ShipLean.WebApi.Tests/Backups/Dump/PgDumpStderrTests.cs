using ShipLean.Application.Features.Backups.Dump;

namespace ShipLean.WebApi.Tests.Backups.Dump;

[TestFixture]
public class PgDumpStderrTests
{
    [Test]
    public void Add_WhenPasswordIsPartOfPrefix_ShouldKeepPrefixAndScrubText()
    {
        var stderr = new PgDumpStderr("dump");

        stderr.Add("pg_dump: error: could not dump, password dump rejected");

        stderr.ToString().ShouldBe("pg_dump: error: could not ***, password *** rejected");
    }

    [Test]
    public void Add_WhenOverLimit_ShouldKeepTailWithinBytes()
    {
        var stderr = new PgDumpStderr(null);

        for (var i = 0; i < 500; i++)
        {
            stderr.Add($"pg_dump: error: line {i} é");
        }
        stderr.Add("pg_dump: error: the last one");

        var text = stderr.ToString();
        System.Text.Encoding.UTF8.GetByteCount(text).ShouldBeLessThanOrEqualTo(PgDumpStderr.MaxBytes);
        text.ShouldEndWith("pg_dump: error: the last one");
        text.ShouldNotContain("line 0 ");
    }
}
