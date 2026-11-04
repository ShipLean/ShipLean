using System.Text;
using ShipLean.Application.Features.Demos.Upload;
using ShipLean.Contracts.Errors;

namespace ShipLean.UnitTest.Demos;

// K8's chapters part: each rule, by the one thing that breaks it.
[TestFixture]
public class DemoChaptersFileTests
{
    private static ReadOnlySpan<byte> Json(string text) => Encoding.UTF8.GetBytes(text);

    private static string Doc(string chapters, string duration = "142.6") => $$"""{"durationSeconds": {{duration}}, "chapters": [{{chapters}}]}""";

    private static ApiException Refused(string json)
    {
        var error = Should.Throw<ApiException>(() => DemoChaptersFile.Parse(Json(json)));
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("chapters");
        return error;
    }

    [Test]
    public void Parse_WhenValid_ShouldKeepEveryChapter()
    {
        var file = DemoChaptersFile.Parse(Json(Doc("""{"title":"Intro","startSeconds":0},{"title":"Checkout v2","startSeconds":15.2,"featureKey":"SHIP-101"}""")));

        file.DurationSeconds.ShouldBe(142.6m);
        file.Chapters.Select(c => c.Title).ShouldBe(["Intro", "Checkout v2"]);
        file.Chapters.Select(c => c.StartSeconds).ShouldBe([0m, 15.2m]);
        file.Chapters.Select(c => c.FeatureKey).ShouldBe([null, "SHIP-101"]);
    }

    [Test]
    public void Parse_WhenTwoHundredChapters_ShouldBeValid()
    {
        var chapters = string.Join(",", Enumerable.Range(0, 200).Select(i => $$"""{"title":"C{{i}}","startSeconds":{{i}}}"""));

        DemoChaptersFile.Parse(Json(Doc(chapters, "1000"))).Chapters.Count.ShouldBe(200);
    }

    [TestCase("not json")]
    [TestCase("")]
    [TestCase("[]")]
    [TestCase("""{"durationSeconds": "long", "chapters": []}""")]
    public void Parse_WhenNotTheShape_ShouldBeValidation(string json)
    {
        var error = Refused(json);

        error.Message.ShouldContain("chapters");
    }

    [Test]
    public void Parse_WhenNoChaptersKey_ShouldBeValidation()
    {
        var error = Refused("""{"durationSeconds": 10}""");

        error.Message.ShouldContain("chapters list");
    }

    [TestCase("0")]
    [TestCase("-3")]
    public void Parse_WhenDurationNotAbove0_ShouldBeValidation(string duration)
    {
        var error = Refused(Doc("""{"title":"Intro","startSeconds":0}""", duration));

        error.Message.ShouldContain("durationSeconds");
    }

    [Test]
    public void Parse_WhenNoChapters_ShouldBeValidation()
    {
        var error = Refused(Doc(string.Empty));

        error.Message.ShouldContain("1 to 200");
    }

    [Test]
    public void Parse_WhenMoreThanTwoHundredChapters_ShouldBeValidation()
    {
        var chapters = string.Join(",", Enumerable.Range(0, 201).Select(i => $$"""{"title":"C{{i}}","startSeconds":{{i}}}"""));

        var error = Refused(Doc(chapters));

        error.Message.ShouldContain("1 to 200");
    }

    [Test]
    public void Parse_WhenFirstNotAt0_ShouldBeValidation()
    {
        var error = Refused(Doc("""{"title":"Intro","startSeconds":0.5}"""));

        error.Message.ShouldContain("first chapter must start at 0");
    }

    [Test]
    public void Parse_WhenNotAscending_ShouldBeValidation()
    {
        var error = Refused(Doc("""{"title":"A","startSeconds":0},{"title":"B","startSeconds":20},{"title":"C","startSeconds":10}"""));

        error.Message.ShouldContain("Chapter 3 must start after chapter 2");
    }

    [Test]
    public void Parse_WhenTwoStartTogether_ShouldBeValidation()
    {
        var error = Refused(Doc("""{"title":"A","startSeconds":0},{"title":"B","startSeconds":5},{"title":"C","startSeconds":5}"""));

        error.Message.ShouldContain("Chapter 3 must start after chapter 2");
    }

    [Test]
    public void Parse_WhenAChapterStartsAtTheDuration_ShouldBeValidation()
    {
        var error = Refused(Doc("""{"title":"A","startSeconds":0},{"title":"B","startSeconds":142.6}"""));

        error.Message.ShouldContain("before the video ends");
    }

    [Test]
    public void Parse_WhenAChapterStartsPastTheDuration_ShouldBeValidation()
    {
        var error = Refused(Doc("""{"title":"A","startSeconds":0},{"title":"B","startSeconds":500}"""));

        error.Message.ShouldContain("before the video ends");
    }

    [TestCase("")]
    [TestCase("   ")]
    public void Parse_WhenTitleBlank_ShouldBeValidation(string title)
    {
        var error = Refused(Doc($$"""{"title":"{{title}}","startSeconds":0}"""));

        error.Message.ShouldContain("title of 1 to 200");
    }

    [Test]
    public void Parse_WhenTitleMissing_ShouldBeValidation()
    {
        var error = Refused(Doc("""{"startSeconds":0}"""));

        error.Message.ShouldContain("title of 1 to 200");
    }

    [Test]
    public void Parse_WhenTitleIs201Characters_ShouldBeValidation()
    {
        var title = new string('a', 201);
        var error = Refused(Doc($$"""{"title":"{{title}}","startSeconds":0}"""));

        error.Message.ShouldContain("title of 1 to 200");
    }

    [Test]
    public void Parse_WhenTitleIs200Characters_ShouldBeValid()
    {
        var title = new string('a', 200);

        DemoChaptersFile.Parse(Json(Doc($$"""{"title":"{{title}}","startSeconds":0}"""))).Chapters.Count.ShouldBe(1);
    }

    [TestCase("a\\nb")]
    [TestCase("a\\u0000b")]
    [TestCase("a\\tb")]
    public void Parse_WhenTitleHoldsAControlCharacter_ShouldBeValidation(string escaped)
    {
        var error = Refused(Doc($$"""{"title":"{{escaped}}","startSeconds":0}"""));

        error.Message.ShouldContain("control character");
    }

    [Test]
    public void Parse_WhenAChapterIsNull_ShouldBeValidation()
    {
        var error = Refused(Doc("null"));

        error.Message.ShouldContain("Chapter 1");
    }

    [Test]
    public void Parse_WhenFeatureKeyBlank_ShouldBeValidation()
    {
        var error = Refused(Doc("""{"title":"A","startSeconds":0,"featureKey":" "}"""));

        error.Message.ShouldContain("featureKey");
    }
}
