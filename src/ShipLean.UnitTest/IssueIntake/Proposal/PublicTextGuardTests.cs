using System.Reflection;
using System.Text.RegularExpressions;
using ShipLean.Application.Features.IssueIntake.Proposal;
using ShipLean.TestSupport;
using Shouldly;

namespace ShipLean.UnitTest.IssueIntake.Proposal;

// D17's last line: every agent-written text passes this guard before it is posted on GitHub. The inputs are what a misled or
// hostile agent might write, so each rule is tried with the encodings and spellings GitHub would still render.
public sealed partial class PublicTextGuardTests
{
    private const string Author = "reporter";

    private static readonly PublicTextGuard Guard = new(["https://prod.shiplean.app/"], ["Joel Dickson", "Ana Silva", "Mark"]);

    [Test]
    public void Guard_WhenMentionsOthers_ShouldNeutralise()
    {
        var result = Guard.Clean("Thanks @reporter and @Reporter. cc @bob, @ShipLean/maintainers and \\@carol.", Author);

        result.Text.ShouldBe("Thanks @reporter and @Reporter. cc `@bob`, `@ShipLean/maintainers` and `@carol`.");
        result.Removed.Count(r => r.StartsWith("mention: ", StringComparison.Ordinal)).ShouldBe(3);
    }

    [Test]
    public void Guard_WhenMentionInCode_ShouldLeaveItAlone()
    {
        const string text = "Run `@bob deploy` and ``@carol``.\n\n```\n@dave\n```\n\n    @erin\n";

        var result = Guard.Clean(text, Author);

        result.Text.ShouldBe(text);
        result.Removed.ShouldBeEmpty();
    }

    [Test]
    public void Guard_WhenStrayBacktickBeforeMention_ShouldStillNeutralise()
    {
        var result = Guard.Clean("a ` b @bob c", Author);

        result.Text.ShouldBe("a ` b `@\u200Bbob` c");
    }

    [TestCase("See https://github.com/ShipLean/ShipLean/pull/47 and SHIP-12.")]
    [TestCase("Docs: [backlogs](https://docs.shiplean.app/backlogs) and https://notprod.shiplean.app.evil.example")]
    [TestCase("x@y and user/@bob and a@b")]
    public void Guard_WhenPublicText_ShouldKeepIt(string text)
    {
        var result = Guard.Clean(text, Author);

        result.Text.ShouldBe(text);
        result.Removed.ShouldBeEmpty();
    }

    [TestCase("See [the item](https://prod.shiplean.app/p/SHIP/items/SHIP-12) now.", "See the item now.")]
    [TestCase("See [the item](HTTPS://PROD.SHIPLEAN.APP/p/SHIP) now.", "See the item now.")]
    [TestCase("See <https://prod.shiplean.app/p/SHIP> now.", "See  now.")]
    [TestCase("See https://prod.shiplean.app/p/SHIP now.", "See  now.")]
    [TestCase("See prod.shiplean.app/p/SHIP now.", "See  now.")]
    [TestCase("See [it](https://prod%2Eshiplean%2Eapp/p) now.", "See it now.")]
    [TestCase("See [it](https://prod%252Eshiplean.app/p) now.", "See it now.")]
    [TestCase("See [it](https://prod&#46;shiplean&#x2E;app/p) now.", "See it now.")]
    [TestCase("See [it](//prod.shiplean.app/p) now.", "See it now.")]
    [TestCase("See [it](https://evil.example/?next=https%3A%2F%2Fprod.shiplean.app) now.", "See it now.")]
    [TestCase("See ![shot](https://prod.shiplean.app/api/a.png) now.", "See shot now.")]
    [TestCase("See [it][r] now.\n\n[r]: https://prod.shiplean.app/p\n", "See it now.\n\n\n")]
    [TestCase("Host: `prod.shiplean.app` here.", "Host:  here.")]
    public void Guard_WhenInstanceLink_ShouldRemove(string text, string expected)
    {
        var result = Guard.Clean(text, Author);

        result.Text.ShouldBe(expected);
        result.Removed.ShouldContain(r => r.StartsWith("instance link: ", StringComparison.Ordinal));
    }

    [TestCase("Done.\n\n<!-- shiplean:outbox:3f2b8c1e-0000-4000-8000-000000000001 -->", "Done.\n\n")]
    [TestCase("Done <!-- shiplean:outbox:3f2b8c1e-0000-4000-8000-000000000001 --> here.", "Done  here.")]
    [TestCase("Done &lt;!-- shiplean:outbox:3f2b8c1e-0000-4000-8000-000000000001 --&gt; here.", "Done  here.")]
    [TestCase("Done.\n\n<!-- shiplean:outbox:3f2b8c1e-0000-4000-8000-000000000001\nnever closed\n@bob", "Done.\n\n")]
    public void Guard_WhenHtmlCommentMarker_ShouldRemove(string text, string expected)
    {
        var result = Guard.Clean(text, Author);

        result.Text.ShouldBe(expected);
        result.Removed.ShouldContain(r => r.StartsWith("HTML: <!-- shiplean:outbox:", StringComparison.Ordinal));
    }

    [TestCase("<script>alert(1)</script>")]
    [TestCase("Hi <img src=x onerror=\"alert(1)\"> there")]
    [TestCase("Hi <IMG SRC=\"javascript:alert(1)\"> there")]
    [TestCase("Hi <<b>script>alert(2)<</b>/script> there")]
    [TestCase("Hi <a href=\"https://evil.example\">click</a> there")]
    [TestCase("<details><summary>Notes</summary>\n\nsecret\n</details>")]
    [TestCase("Hi &lt;script&gt;alert(3)&lt;/script&gt; there")]
    [TestCase("Hi &amp;lt;b&amp;gt;bold&amp;lt;/b&amp;gt; there")]
    [TestCase("<?php echo 1; ?> and <![CDATA[x]]> and <!DOCTYPE html>")]
    public void Guard_WhenHtml_ShouldRemoveEveryTag(string text)
    {
        var result = Guard.Clean(text, Author);

        HtmlTag().IsMatch(result.Text).ShouldBeFalse(result.Text);
        result.Removed.ShouldContain(r => r.StartsWith("HTML: ", StringComparison.Ordinal));
    }

    [Test]
    public void Guard_WhenAngleBracketsInCode_ShouldKeepThem()
    {
        const string text = "Use `List<string>` and\n\n```csharp\nvar x = new Dictionary<string, int>();\n```\n";

        var result = Guard.Clean(text, Author);

        result.Text.ShouldBe(text);
    }

    [TestCase("Ask joel@example.com today.", "Ask  today.")]
    [TestCase("Ask Joel.Dickson+ship@mail.example.co.uk today.", "Ask  today.")]
    [TestCase("Ask joel @ example.com today.", "Ask  today.")]
    [TestCase("Ask joel [at] example [dot] com today.", "Ask  today.")]
    [TestCase("Ask joel(AT)example(DOT)com today.", "Ask  today.")]
    [TestCase("Ask joel {at} example.com today.", "Ask  today.")]
    [TestCase("Ask joel&#64;example&#46;com today.", "Ask  today.")]
    [TestCase("Ask <mailto:joel@example.com> today.", "Ask <mailto:> today.")]
    [TestCase("Ask `joel@example.com` today.", "Ask `` today.")]
    [TestCase("Ask Joel Dickson today.", "Ask  today.")]
    [TestCase("Ask JOEL\nDICKSON today.", "Ask  today.")]
    [TestCase("Ask ana silva and ANA   SILVA today.", "Ask  and  today.")]
    [TestCase("Ask An&#97; Silva today.", "Ask  today.")]
    public void Guard_WhenEmailOrMemberName_ShouldRemove(string text, string expected)
    {
        var result = Guard.Clean(text, Author);

        result.Text.ShouldBe(expected);
        result.Removed.ShouldNotBeEmpty();
    }

    [TestCase("https://github.io.example.com/path", true)]
    [TestCase("https://x.github.io/a", false)]
    [TestCase("https://github.io/a", false)]
    [TestCase("https://shiplean.example.com.evil.net/", true)]
    [TestCase("https://sub.shiplean.example.com/", false)]
    public void Guard_WhenHostOnlyStartsWithInstance_ShouldKeepIt(string url, bool survives)
    {
        var guard = new PublicTextGuard([url.Contains("shiplean") ? "https://shiplean.example.com/" : "https://github.io/"], []);

        var result = guard.Clean($"See {url} now.", Author);

        result.Text.Contains(url, StringComparison.Ordinal).ShouldBe(survives, string.Join(" | ", result.Removed));
        result.Safe.ShouldBeTrue(string.Join(" | ", result.Removed));
    }

    [Test]
    public void Guard_WhenSingleWordMemberName_ShouldKeepOrdinaryWord()
    {
        var result = Guard.Clean("Mark this issue as complete", Author);

        result.Safe.ShouldBeTrue(string.Join(" | ", result.Removed));
        result.Text.ShouldBe("Mark this issue as complete");
    }

    [TestCase("Mark Smith said hi")]
    [TestCase("Mark **Smith** said hi")]
    public void Guard_WhenTwoWordMemberName_ShouldRemoveOrFailClosed(string text)
    {
        var guard = new PublicTextGuard(["https://prod.shiplean.app/"], ["Mark Smith"]);

        var result = guard.Clean(text, Author);

        (result.Safe ? result.Text : string.Empty).ShouldNotContain("Smith");
    }

    [Test]
    public void Guard_WhenNameInsideWord_ShouldKeepIt()
    {
        var result = Guard.Clean("Banana bread and Anatomy.", Author);

        result.Text.ShouldBe("Banana bread and Anatomy.");
    }

    [Test]
    public void Guard_WhenOverCap_ShouldCutTo60000Characters()
    {
        var result = Guard.Clean(new string('x', 70_000), Author);

        result.Text.Length.ShouldBe(PublicTextGuard.MaxLength);
        result.Text.ShouldEndWith("posts are limited to 60,000 characters.)");
        result.Removed.ShouldContain(r => r.StartsWith("cut: ", StringComparison.Ordinal));
    }

    [Test]
    public void Guard_WhenMentionsGrowTextPastCap_ShouldStillCut()
    {
        var text = string.Concat(Enumerable.Repeat("@bob ", 11_999));

        var result = Guard.Clean(text, Author);

        text.Length.ShouldBeLessThan(PublicTextGuard.MaxLength);
        result.Text.Length.ShouldBeLessThanOrEqualTo(PublicTextGuard.MaxLength);
    }

    [Test]
    public void Guard_WhenInjectionSample_ShouldKeepOnlyPlainText()
    {
        var sample = File.ReadAllText(Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "IssueIntake", "Proposals", "injection.md"));

        var text = Guard.Clean(sample, Author).Text;

        HtmlTag().IsMatch(text.Replace("`List<string>`", string.Empty, StringComparison.Ordinal)).ShouldBeFalse(text);
        text.ShouldNotContain("shiplean:outbox");
        text.ShouldNotContain("evil.example");
        Uri.UnescapeDataString(text).ShouldNotContain("prod.shiplean.app", Case.Insensitive);
        text.ShouldNotContain("Dickson", Case.Sensitive);
        text.ShouldNotContain("joel [at]", Case.Sensitive);
        text.ShouldNotContain("example.com");
        text.ShouldNotContain("hidden-mention");
        text.ShouldContain("Thanks @reporter, here is the proposal.");
        text.ShouldContain("cc `@joeldickson` `@ShipLean/maintainers`");
        text.ShouldContain("Use `@not-a-mention` in code, and `List<string>` stays.");
        text.ShouldContain("See the item, again,");
    }

    // G1: a subdomain of the instance is the instance, in every spelling a browser accepts.
    [TestCase("prod", "See https://evil.prod.shiplean.app/x now.", true)]
    [TestCase("prod", "See [it](https://a.b.PROD.shiplean.app./x) now.", true)]
    [TestCase("prod", "See https://evil.prod.shiplean.app:8443/x now.", true)]
    [TestCase("prod", "See https://user:pw@evil.prod.shiplean.app/x now.", true)]
    [TestCase("prod", "See https://evil.ｐｒｏｄ.shiplean.app/x now.", false)]
    [TestCase("prod", "See evil.prod.**shiplean**.app now.", false)]
    [TestCase("idn", "See https://evil.xn--bcher-kva.example/x now.", true)]
    [TestCase("idn", "See https://evil.bücher.example:8443/x now.", true)]
    [TestCase("punycode", "See https://user@evil.bücher.example/x now.", true)]
    [TestCase("ip", "See http://10.0.0.5:8080/x now.", true)]
    [TestCase("ip", "See http://167772165/x now.", false)]
    public void Guard_WhenInstanceSubdomainOrOtherSpelling_ShouldRemoveOrFailClosed(string instance, string text, bool safe)
    {
        var guard = instance switch
        {
            "idn" => new PublicTextGuard(["https://bücher.example/"], []),
            "punycode" => new PublicTextGuard(["https://xn--bcher-kva.example/"], []),
            "ip" => new PublicTextGuard(["http://10.0.0.5:8080/"], []),
            _ => Guard,
        };

        var result = guard.Clean(text, Author);

        result.Safe.ShouldBe(safe, string.Join(" | ", result.Removed));
        if (safe)
        {
            result.Text.ShouldStartWith("See ");
            result.Text.ShouldEndWith(" now.");
            result.Text.Length.ShouldBeLessThanOrEqualTo("See it now.".Length);
        }
        else
        {
            result.Text.ShouldBeEmpty();
            result.Removed[^1].ShouldStartWith("unsafe: instance host ");
        }
    }

    // G2: names and emails are checked against what GitHub shows, with emphasis and zero-width characters gone.
    [TestCase("Ask Joel **Dickson** today.", "unsafe: member name Joel Dickson")]
    [TestCase("Ask J*oel* Dickson today.", "unsafe: member name Joel Dickson")]
    [TestCase("Ask J\u200Boel Dickson today.", "unsafe: member name Joel Dickson")]
    [TestCase("Ask joel@**example**.com today.", "unsafe: email ")]
    [TestCase("Ask joel@ex*am*ple.com today.", "unsafe: email ")]
    public void Guard_WhenNameOrEmailSplitByEmphasis_ShouldFailClosed(string text, string reason)
    {
        var result = Guard.Clean(text, Author);

        result.Safe.ShouldBeFalse();
        result.Text.ShouldBeEmpty();
        result.Removed[^1].ShouldStartWith(reason);
    }

    // G3: an outbox marker anywhere, code included, could spoof the raw-body marker check.
    [TestCase("Done `<!-- shiplean:outbox:3f2b8c1e-0000-4000-8000-000000000001 -->` here.")]
    [TestCase("Done.\n\n```\n<!-- shiplean:outbox:3f2b8c1e-0000-4000-8000-000000000001 -->\n```\n")]
    [TestCase("Done.\n\n    SHIPLEAN:OUTBOX:3f2b8c1e-0000-4000-8000-000000000001\n")]
    public void Guard_WhenOutboxMarkerInCode_ShouldFailClosed(string text)
    {
        var result = Guard.Clean(text, Author);

        result.Safe.ShouldBeFalse();
        result.Text.ShouldBeEmpty();
        result.Removed[^1].ShouldBe("unsafe: an outbox marker");
    }

    // G4: wrapping the mentions grows the text past the cap, and the cut lands somewhere in `@carol`.
    [Test]
    public void Guard_WhenCapCutsNeutralisedMention_ShouldLeaveNoLiveMention([Range(0, 12)] int pad)
    {
        // Wrapped, the 8,561 mentions fill 59,927 characters; the cap keeps 59,939, so each pad moves the cut one character
        // through `@carol`.
        var text = string.Concat(Enumerable.Repeat("@bob ", 8_561)) + new string('x', pad) + "`@carol` " + new string('y', 200);

        var result = Guard.Clean(text, Author);

        result.Safe.ShouldBeTrue(string.Join(" | ", result.Removed.TakeLast(3)));
        result.Text.Length.ShouldBeLessThanOrEqualTo(PublicTextGuard.MaxLength);
        result.Text.ShouldEndWith("posts are limited to 60,000 characters.)");
        result.Text.Replace("`@carol`", string.Empty, StringComparison.Ordinal).ShouldNotContain("@c");
    }

    // G5: too deep, too big or encoded too many times fails closed, and never throws.
    [TestCase("deep")]
    [TestCase("big")]
    [TestCase("encoded")]
    public void Guard_WhenInputPastBounds_ShouldFailClosedWithoutThrowing(string bound)
    {
        var text = bound switch
        {
            "deep" => new string('>', 20_000) + " deep",
            "big" => new string('x', PublicTextGuard.MaxInput + 1),
            _ => "&" + string.Concat(Enumerable.Repeat("amp;", 20)) + "lt;b&gt;",
        };

        var result = Guard.Clean(text, Author);

        result.Safe.ShouldBeFalse();
        result.Text.ShouldBeEmpty();
        result.Removed[^1].ShouldStartWith("unsafe: ");
    }

    // Clean is a fixed point over every attacker input in this class: cleaning its own output changes nothing.
    [TestCaseSource(nameof(Corpus))]
    public void Guard_WhenCleanedTwice_ShouldChangeNothing(string text)
    {
        var first = Guard.Clean(text, Author);

        var second = Guard.Clean(first.Text, Author);

        second.Text.ShouldBe(first.Text);
        second.Safe.ShouldBeTrue();
        second.Removed.ShouldBeEmpty();
    }

    private static IEnumerable<TestCaseData> Corpus()
    {
        var cases = typeof(PublicTextGuardTests).GetMethods()
            .SelectMany(m => m.GetCustomAttributes<TestCaseAttribute>())
            .Select(a => a.Arguments.FirstOrDefault())
            .OfType<string>()
            .Where(t => t.Length > 12)
            .Concat(
            [
                File.ReadAllText(Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "IssueIntake", "Proposals", "injection.md")),
                "Thanks @reporter and @Reporter. cc @bob, @ShipLean/maintainers and \\@carol.",
                "Run `@bob deploy` and ``@carol``.\n\n```\n@dave\n```\n\n    @erin\n",
                "a ` b @bob c",
                "Banana bread and Anatomy.",
                new string('x', 70_000),
                string.Concat(Enumerable.Repeat("@bob ", 11_999)),
                string.Concat(Enumerable.Repeat("@bob ", 8_561)) + "xxxxxxx`@carol` " + new string('y', 200),
            ])
            .ToList();
        return cases.Select((text, i) => new TestCaseData(text).SetArgDisplayNames(i.ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }

    [GeneratedRegex("<[A-Za-z!/?]")]
    private static partial Regex HtmlTag();
}
