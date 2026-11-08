using System.Globalization;
using ShipLean.Application.Features.IssueIntake.Proposal;
using ShipLean.TestSupport;
using Shouldly;

namespace ShipLean.UnitTest.IssueIntake.Proposal;

// SHIP-751: a misled agent could put a secret it read into its proposal, so secret-shaped text makes the whole post unsafe.
// Every fake secret is built from pieces at run time (never a full literal), and the test names show the kind, not the secret.
public sealed class PublicTextGuardSecretTests
{
    private const string Author = "reporter";
    private const string Base62 = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    private static readonly PublicTextGuard Guard = new(["https://prod.shiplean.app/"], ["Joel Dickson"]);
    private static readonly string Goldens = Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "IssueIntake", "Proposals");

    [TestCaseSource(nameof(Secrets))]
    public void Guard_WhenSecret_ShouldFailClosedNamingOnlyTheKind(string secret, string kind)
    {
        var result = Guard.Clean($"Here is the fix.\n\n{secret}\n\nThanks!", Author);

        result.Safe.ShouldBeFalse();
        result.Text.ShouldBeEmpty();
        result.Removed.ShouldHaveSingleItem().ShouldBe($"unsafe: secret-shaped text: {kind}");
    }

    [TestCaseSource(nameof(Hidden))]
    public void Guard_WhenTokenEncodedOrSplit_ShouldFailClosed(string text, string token)
    {
        var result = Guard.Clean(text, Author);

        result.Safe.ShouldBeFalse();
        result.Text.ShouldBeEmpty();
        result.Removed[^1].ShouldBe("unsafe: secret-shaped text: GitHub token");
        var piece = token.Substring(4, 16);
        result.Removed.ShouldAllBe(r => !r.Contains(piece));
    }

    [TestCase("Fixed in 3f2b8c1 and 3f2b8c1e9a0d4b7c8e1f2a3b4c5d6e7f8a9b0c1d.")]
    [TestCase("Fixed in 3F2B8C1E9A0D4B7C8E1F2A3B4C5D6E7F8A9B0C1D.")]
    [TestCase("See https://github.com/ShipLean/ShipLean/commit/3f2b8c1e9a0d4b7c8e1f2a3b4c5d6e7f8a9b0c1d now.")]
    [TestCase("See https://github.com/ShipLean/ShipLean/compare/3f2b8c1e9a0d4b7c8e1f2a3b4c5d6e7f8a9b0c1d...8e1f2a3b4c5d6e7f8a9b0c1d3f2b8c1e9a0d4b7c now.")]
    [TestCase("Ids 3F2504E0-4F89-11D3-9A0C-0305E82C3301 and 3f2504e0-4f89-11d3-9a0c-0305e82c3301.")]
    [TestCase("Run https://github.com/ShipLean/ShipLean/actions/runs/19234567890/job/54123456789 and PR https://github.com/ShipLean/ShipLean/pull/80.")]
    [TestCase("Image ghcr.io/shiplean/shiplean:3f2b8c1e9a0d4b7c8e1f2a3b4c5d6e7f8a9b0c1d, version 1.0.0+3f2b8c1e9a0d4b7c8e1f2a3b4c5d6e7f8a9b0c1d.")]
    [TestCase("See GitHubIssueOutboxDeliveryServiceTests and Guard_WhenInstanceSubdomainOrOtherSpelling_ShouldRemoveOrFailClosed.")]
    [TestCase("See Base64UrlEncoderV2ServiceTestsForIssue123, Sha256HashComputationForUtf8Bytes1024 and X509Certificate2CollectionExtensionsV3.")]
    [TestCase("Edit `src/ShipLean.Application/Features/IssueIntake/Proposal/PublicTextGuard.cs` and `ShipLean.UnitTest.IssueIntake.Proposal`.")]
    [TestCase("The header `SGVsbG8gd29ybGQ=` decodes to Hello world; words like Base64, README2 and Dockerfile stay.")]
    [TestCase("Branches task-runner-for-github-issue-outbox-delivery-v2 and sk-learn are fine.")]
    [TestCase("Set `aws_secret_access_key` in your own profile. The ghp_ prefix marks a classic token, eyJ starts a JWT.")]
    [TestCase("Paste the -----BEGIN PUBLIC KEY----- or -----BEGIN CERTIFICATE----- block, never a private one.")]
    public void Guard_WhenOnlySecretLooking_ShouldKeepIt(string text)
    {
        var result = Guard.Clean(text, Author);

        result.Safe.ShouldBeTrue(string.Join(" | ", result.Removed));
        result.Text.ShouldBe(text);
    }

    [TestCaseSource(nameof(GoldenNames))]
    public void Guard_WhenGolden_ShouldStaySafe(string name)
    {
        var response = File.ReadAllText(Path.Combine(Goldens, name));
        var text = IssueProposalParser.TryParse(response, out _, out var publicMarkdown) ? publicMarkdown : response;

        var result = Guard.Clean(text, Author);

        result.Safe.ShouldBeTrue(string.Join(" | ", result.Removed));
    }

    private static IEnumerable<string> GoldenNames()
    {
        return Directory.GetFiles(Goldens, "*.md").Select(f => Path.GetFileName(f)).Order(StringComparer.Ordinal);
    }

    private static IEnumerable<TestCaseData> Secrets()
    {
        var github = new[] { "p", "o", "u", "s", "r" }.Select(c => ("gh" + c + "_" + Fake(36, 1), "GitHub token"));
        var keys = new[] { "RSA ", "EC ", "OPENSSH ", string.Empty }.Select(k => ("-----BEGIN " + k + "PRIVATE" + " KEY-----", "private key"));
        (string Secret, string Kind)[] others =
        [
            ("github" + "_pat_" + "11ABCDEFG0" + "_" + Fake(59, 2), "GitHub token"),
            ("AK" + "IA" + Fake(16, 3).ToUpperInvariant(), "AWS access key id"),
            ("AS" + "IA" + Fake(16, 4).ToUpperInvariant(), "AWS access key id"),
            ("aws_secret_access_key = " + Fake(38, 5) + "+/", "AWS secret access key"),
            ("AWS_SECRET_ACCESS_KEY=\"" + Fake(40, 6) + "\"", "AWS secret access key"),
            ("xo" + "xb-" + "1234567890-" + Fake(24, 7), "Slack token"),
            ("xo" + "xp-" + "1234567890-" + Fake(24, 8), "Slack token"),
            ("https://hooks.slack" + ".com/services/T0000/B0000/" + Fake(24, 9), "Slack webhook URL"),
            ("sk-" + "ant-api03-" + Fake(40, 10), "Anthropic API key"),
            ("sk-" + Fake(48, 11), "sk- API key"),
            ("sk-" + "proj-" + Fake(48, 12), "sk- API key"),
            ("ey" + "JhbGciOiJIUzI1NiJ9" + ".ey" + "JzdWIiOiIxMjM0In0." + Fake(43, 13), "JSON web token"),
            ("ey" + "JhbGciOiJub25lIn0" + ".ey" + "JzdWIiOiIxMjM0In0.", "JSON web token"),
            ("-----BEGIN PGP " + "PRIVATE" + " KEY BLOCK-----", "private key"),
            (Fake(40, 14), "high-entropy string"),
            (Fake(20, 15) + "+" + Fake(19, 16) + "=", "high-entropy string"),
        ];
        return github.Concat(keys).Concat(others)
            .Select((s, i) => new TestCaseData(s.Item1, s.Item2).SetArgDisplayNames(i.ToString(CultureInfo.InvariantCulture) + ": " + s.Item2));
    }

    // One GitHub token, hidden the ways GitHub would still show it whole, or tucked where the removal passes would drop it.
    private static IEnumerable<TestCaseData> Hidden()
    {
        var body = Fake(36, 20);
        var token = "gh" + "p_" + body;
        (string Text, string Way)[] cases =
        [
            ("Fix: gh" + "p&#95;" + body, "entity"),
            ("Fix: &#103;h" + "p&#x5F;" + body, "hex entity"),
            ("Fix: gh" + "p%5F" + body, "percent"),
            ("Fix: gh**" + "p_**" + body, "strong split"),
            ("Fix: gh*" + "p*_" + body, "emphasis split"),
            ("Fix: ~~gh~~" + "p_" + body, "strike split"),
            ("Fix: gh​" + "p_" + body, "zero-width space"),
            ("Fix: gh" + "p_" + body[..10] + "­" + body[10..], "soft hyphen"),
            ("Fix: gh" + "p\\_" + body, "escaped underscore"),
            ("Fix: gh<b></b>" + "p_" + body, "split by a tag"),
            ("Fix:\n\n<div>\n" + token + "\n</div>\n", "inside an HTML block"),
            ("Fix: <!-- " + token + " -->", "inside an HTML comment"),
            ("Fix: [the item](https://prod.shiplean.app/p?t=" + token + ")", "inside an instance link"),
            ("Fix: `" + token + "`", "code span"),
            ("Fix:\n\n```\nexport GH_TOKEN=" + token + "\n```\n", "code block"),
        ];
        return cases.Select(c => new TestCaseData(c.Text, token).SetArgDisplayNames(c.Way));
    }

    // A fixed, random-looking base62 string (a linear congruential generator), so no secret-shaped literal is committed.
    private static string Fake(int length, int seed)
    {
        var state = (uint)seed * 2_654_435_761u;
        var chars = new char[length];
        for (var i = 0; i < length; i++)
        {
            state = (state * 1_664_525u) + 1_013_904_223u;
            chars[i] = Base62[(int)((state >> 16) % Base62.Length)];
        }

        return new string(chars);
    }
}
