using System.Text.Json.Nodes;
using ShipLean.TestSupport;
using ShipLean.UnitTest.Dtos.Projects;

namespace ShipLean.UnitTest.Dtos.Demos;

// K15: the pinned demo fixtures obey the status and brand rules of K4 and K6, so a page shot or component test never blesses impossible data.
[TestFixture]
public class DemoFixturesObeyContractTests
{
    private static JsonNode Read(string file) => ProjectDtoFixture.Read("demos", file);

    private static bool Has(JsonNode node, string property) => node[property] is not null;

    [TestCase("sprint-demo-ready.json")]
    [TestCase("sprint-demo-youtube-unlisted.json")]
    [TestCase("sprint-demo-youtube-private.json")]
    [TestCase("sprint-demo-youtube-failed.json")]
    public void Ready_WhenFixtureIsReady_ShouldHaveAVideoAndAScript(string file)
    {
        var dto = Read(file);

        dto["status"]!.GetValue<string>().ShouldBe("ready");
        Has(dto, "video").ShouldBeTrue();
        Has(dto, "scriptHtml").ShouldBeTrue();
        dto["video"]!["contentUrl"]!.GetValue<string>().ShouldBe($"/api/attachments/{dto["video"]!["attachmentId"]!.GetValue<string>()}/content");
        dto["chapters"]!.AsArray().Count.ShouldBeGreaterThan(0);
        dto["features"]!.AsArray().Count.ShouldBeGreaterThan(0);
    }

    [Test]
    public void None_WhenFixtureIsNone_ShouldHaveNeitherVideoNorScript()
    {
        var dto = Read("sprint-demo-none.json");

        Has(dto, "video").ShouldBeFalse();
        Has(dto, "scriptHtml").ShouldBeFalse();
        Has(dto, "run").ShouldBeFalse();
        dto["chapters"]!.AsArray().ShouldBeEmpty();
    }

    [Test]
    public void Working_WhenFixtureIsWorking_ShouldHaveARunAndNoCreate()
    {
        var dto = Read("sprint-demo-working.json");

        Has(dto, "run").ShouldBeTrue();
        dto["actions"]!["canCreate"]!.GetValue<bool>().ShouldBeFalse();
        Has(dto["actions"]!, "createBlockedMessage").ShouldBeTrue();
    }

    [Test]
    public void YouTube_WhenPrivate_ShouldHaveNoEmbedUrl()
    {
        var youTube = Read("sprint-demo-youtube-private.json")["youTube"]!;

        youTube["privacy"]!.GetValue<string>().ShouldBe("private");
        Has(youTube, "embedUrl").ShouldBeFalse();
    }

    [Test]
    public void YouTube_WhenUnlisted_ShouldHaveTheNoCookieEmbedUrl()
    {
        var youTube = Read("sprint-demo-youtube-unlisted.json")["youTube"]!;

        youTube["embedUrl"]!.GetValue<string>().ShouldStartWith("https://www.youtube-nocookie.com/embed/");
        Has(youTube, "watchUrl").ShouldBeTrue();
    }

    [Test]
    public void YouTube_WhenFailed_ShouldCarryAMessageAndAllowRetry()
    {
        var dto = Read("sprint-demo-youtube-failed.json");

        dto["youTube"]!["state"]!.GetValue<string>().ShouldBe("failed");
        Has(dto["youTube"]!, "failureMessage").ShouldBeTrue();
        dto["actions"]!["canRetryYouTube"]!.GetValue<bool>().ShouldBeTrue();
    }

    [Test]
    public void Brand_WhenDefault_ShouldHaveNoLogoUrl()
    {
        var brand = Read("demo-brand-default.json");

        brand["logoIsDefault"]!.GetValue<bool>().ShouldBeTrue();
        Has(brand, "logoSvg").ShouldBeTrue();
        Has(brand, "logoUrl").ShouldBeFalse();
        Has(brand, "logoFallbackReason").ShouldBeFalse();
    }

    [Test]
    public void Brand_WhenTheProjectsOwn_ShouldHaveALinkAndNoInlineLogo()
    {
        var brand = Read("demo-brand.json");

        brand["logoIsDefault"]!.GetValue<bool>().ShouldBeFalse();
        Has(brand, "logoUrl").ShouldBeTrue();
        Has(brand, "logoSvg").ShouldBeFalse();
        Has(brand, "logoUrlExpiresAt").ShouldBeTrue();
    }

    [Test]
    public void Brand_WhenLogoFellBack_ShouldHaveTheInlineLogoAndAReason()
    {
        var brand = Read("demo-brand-logo-fallback.json");

        Has(brand, "logoSvg").ShouldBeTrue();
        Has(brand, "logoUrl").ShouldBeFalse();
        Has(brand, "logoFallbackReason").ShouldBeTrue();
    }

    [TestCase("project-demo-settings.json")]
    [TestCase("project-demo-settings-branded.json")]
    public void Settings_WhenFixture_ShouldListTheSevenDefaultTokens(string file)
    {
        var names = Read(file)["brand"]!["defaultTokens"]!.AsArray().Select(t => t!["name"]!.GetValue<string>()).ToList();

        names.ShouldBe(["--color-bg", "--color-surface", "--color-text", "--color-muted", "--color-accent", "--color-accent-fg", "--font-sans"]);
    }

    [Test]
    public void Chapters_WhenFixture_ShouldStartAtZeroAscendingBelowTheDuration()
    {
        var doc = Read("demo-chapters.json");
        var starts = doc["chapters"]!.AsArray().Select(c => c!["startSeconds"]!.GetValue<decimal>()).ToList();

        starts[0].ShouldBe(0);
        starts.ShouldBe([.. starts.Order()]);
        starts.Distinct().Count().ShouldBe(starts.Count);
        starts.ShouldAllBe(s => s < doc["durationSeconds"]!.GetValue<decimal>());
    }

    [Test]
    public void VideoFixture_WhenRead_ShouldBeTheMeasuredMp4()
    {
        var bytes = File.ReadAllBytes(Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "DemoFixtures", "demo-fixture.mp4"));

        bytes.Length.ShouldBe(50_841);
        System.Text.Encoding.ASCII.GetString(bytes, 4, 4).ShouldBe("ftyp");
    }

    [Test]
    public void ReadyFixture_WhenCompared_ShouldHaveAVideoSizeThatMatchesTheFixtureFile()
    {
        Read("sprint-demo-ready.json")["video"]!["sizeBytes"]!.GetValue<long>().ShouldBe(50_841);
    }
}
