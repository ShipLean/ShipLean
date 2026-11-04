using System.Text;
using ShipLean.Application.Images;

namespace ShipLean.UnitTest.Demos;

// The shared SVG sanitiser (attachments and a project's logo): each hostile construct must come out of the cleaned document.
[TestFixture]
public class SvgSanitizerVectorsTests
{
    private const string SvgNs = "http://www.w3.org/2000/svg";

    private static string Clean(string inner, string rootAttributes = "")
    {
        var svg = Encoding.UTF8.GetBytes($"<svg xmlns=\"{SvgNs}\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" {rootAttributes}>{inner}</svg>");
        using var stream = new MemoryStream(svg);
        SvgSanitizer.TrySanitize(stream, out var clean).ShouldBeTrue();
        return Encoding.UTF8.GetString(clean);
    }

    [TestCase("<use href=\"#x\" xml:base=\"https://evil.example/x.svg\"/>", "")]
    [TestCase("<use xlink:href=\"#x\" xml:base=\"//evil.example/x.svg\"/>", "")]
    [TestCase("<g xml:base=\"https://evil.example/\"><use href=\"#x\"/></g>", "")]
    [TestCase("<use href=\"#x\"/>", "xml:base=\"https://evil.example/x.svg\"")]
    [TestCase("<use href=\"#x\" xml:BASE=\"https://evil.example/x.svg\"/>", "")]
    [TestCase("<use href=\"#x\" base=\"https://evil.example/x.svg\"/>", "")]
    public void TrySanitize_WhenXmlBaseOnAnyElement_ShouldDropIt(string inner, string rootAttributes)
    {
        var clean = Clean(inner, rootAttributes);

        clean.ShouldNotContain("evil.example");
        clean.ShouldNotContain("base", Case.Insensitive);
    }

    [Test]
    public void TrySanitize_WhenFragmentLinks_ShouldKeepThem()
    {
        var clean = Clean("<defs><rect id=\"x\" width=\"2\" height=\"2\"/></defs><use href=\"#x\"/><use xlink:href=\"#x\"/>");

        clean.ShouldContain("href=\"#x\"");
        clean.ShouldContain("xlink:href=\"#x\"");
    }

    [TestCase("<use href=\"https://evil.example/a.svg#x\"/>")]
    [TestCase("<use xlink:href=\"//evil.example/a.svg#x\"/>")]
    [TestCase("<use href=\"data:image/svg+xml,%3Csvg/%3E\"/>")]
    [TestCase("<image href=\"http://evil.example/x.png\"/>")]
    [TestCase("<image xlink:href=\"http://evil.example/x.png\"/>")]
    [TestCase("<a href=\"javascript:alert(1)\"><rect/></a>")]
    [TestCase("<a href=\"&#106;avascript&#58;alert(1)\"><rect/></a>")]
    [TestCase("<a xlink:href=\"&#x6A;avascript:alert(1)\"><rect/></a>")]
    [TestCase("<a href=\" \tjavascript:alert(1)\"><rect/></a>")]
    [TestCase("<style>rect { fill: url(https://evil.example/x) }</style>")]
    [TestCase("<style>@import url(https://evil.example/x.css);</style>")]
    [TestCase("<style>@import \"https://evil.example/x.css\";</style>")]
    [TestCase("<style><![CDATA[rect { fill: url('https://evil.example/x') }]]></style>")]
    [TestCase("<style>rect { fill: \\75 rl(https://evil.example/x) }</style>")]
    [TestCase("<style>@\\69mport 'https://evil.example/x.css';</style>")]
    [TestCase("<style>rect { background: image-set(\"https://evil.example/x\" 1x) }</style>")]
    [TestCase("<rect style=\"fill:url(https://evil.example/x)\"/>")]
    [TestCase("<rect style=\"fill:URL( 'https://evil.example/x' )\"/>")]
    [TestCase("<rect style=\"fill:&#117;rl(https://evil.example/x)\"/>")]
    [TestCase("<rect style=\"fill:\\75rl(https://evil.example/x)\"/>")]
    [TestCase("<rect fill=\"url(https://evil.example/x)\"/>")]
    [TestCase("<style>rect { background: image(\"https://evil.example/x\") }</style>")]
    [TestCase("<rect style=\"background: image(&quot;https://evil.example/x&quot;)\"/>")]
    [TestCase("<style>rect { background: image('https://evil.example/x') }</style>")]
    [TestCase("<rect style=\"background: image('https://evil.example/x')\"/>")]
    [TestCase("<style>rect { background: -webkit-image-set(\"https://evil.example/x\" 1x) }</style>")]
    [TestCase("<rect style=\"background: -webkit-image-set(&quot;https://evil.example/x&quot; 1x)\"/>")]
    [TestCase("<style>rect { background: -moz-image-set(\"https://evil.example/x\" 1x) }</style>")]
    [TestCase("<rect style=\"background: -moz-image-set(&quot;https://evil.example/x&quot; 1x)\"/>")]
    [TestCase("<style>rect { background: -webkit-image(\"https://evil.example/x\") }</style>")]
    [TestCase("<rect style=\"background: -webkit-image(&quot;https://evil.example/x&quot;)\"/>")]
    [TestCase("<style>rect { background: cross-fade(\"https://evil.example/x\", 50%) }</style>")]
    [TestCase("<rect style=\"background: cross-fade(&quot;https://evil.example/x&quot;, 50%)\"/>")]
    [TestCase("<style>rect { background: -webkit-cross-fade(\"https://evil.example/x\", 50%) }</style>")]
    [TestCase("<rect style=\"background: -webkit-cross-fade(&quot;https://evil.example/x&quot;, 50%)\"/>")]
    [TestCase("<style>rect { background: element(#a) }</style>")]
    [TestCase("<rect style=\"background: element(#a)\"/>")]
    [TestCase("<style>rect { background: paint(\"https://evil.example/x\") }</style>")]
    [TestCase("<rect style=\"background: paint(&quot;https://evil.example/x&quot;)\"/>")]
    [TestCase("<style>rect { background: -x-src(\"https://evil.example/x\") }</style>")]
    [TestCase("<rect style=\"background: -x-src(&quot;https://evil.example/x&quot;)\"/>")]
    [TestCase("<style>rect { content: \"https://evil.example/x\" }</style>")]
    [TestCase("<rect style=\"cursor: https://evil.example/x\"/>")]
    [TestCase("<rect style=\"background: IMAGE(&quot;https://evil.example/x&quot;)\"/>")]
    [TestCase("<style>rect { background: -WebKit-Image-Set(&quot;https://evil.example/x&quot; 1x) }</style>")]
    [TestCase("<foreignObject><div xmlns=\"http://www.w3.org/1999/xhtml\">evil.example</div></foreignObject>")]
    [TestCase("<script>evil.example</script>")]
    [TestCase("<rect onclick=\"evil.example\"/>")]
    public void TrySanitize_WhenHostileConstruct_ShouldRemoveIt(string inner)
    {
        var clean = Clean(inner);

        clean.ShouldNotContain("evil.example");
        clean.ShouldNotContain("javascript", Case.Insensitive);
        clean.ShouldNotContain("foreignObject");
        clean.ShouldNotContain("image(", Case.Insensitive);
        clean.ShouldNotContain("image-set(", Case.Insensitive);
        clean.ShouldNotContain("cross-fade(", Case.Insensitive);
        clean.ShouldNotContain("element(", Case.Insensitive);
        clean.ShouldNotContain("paint(", Case.Insensitive);
        clean.ShouldNotContain("src(", Case.Insensitive);
    }

    [Test]
    public void TrySanitize_WhenLocalUrlReference_ShouldKeepIt()
    {
        var clean = Clean("<defs><linearGradient id=\"g\"/></defs><rect fill=\"url(#g)\" style=\"stroke:url(#g)\"/>");

        clean.ShouldContain("fill=\"url(#g)\"");
        clean.ShouldContain("stroke:url(#g)");
    }

    [Test]
    public void TrySanitize_WhenPlainQuotedFont_ShouldKeepIt()
    {
        var clean = Clean("<style>text { font-family: \"Inter\", sans-serif }</style><text style=\"font-family:&quot;Inter&quot;\">a</text><text font-family=\"Inter\">b</text>");

        clean.ShouldContain("<style>");
        clean.ShouldContain("font-family: \"Inter\", sans-serif");
        clean.ShouldContain("font-family:&quot;Inter&quot;");
        clean.ShouldContain("font-family=\"Inter\"");
    }

    [Test]
    public void TrySanitize_WhenFragmentUrlInStyleElement_ShouldKeepIt()
    {
        var clean = Clean("<defs><linearGradient id=\"g\"/></defs><style>rect { fill: url(#g) }</style><rect style=\"fill:url('#g')\"/>");

        clean.ShouldContain("<style>rect { fill: url(#g) }</style>");
        clean.ShouldContain("fill:url('#g')");
    }

    [Test]
    public void TrySanitize_ShouldKeepNamespaceDeclarations()
    {
        var clean = Clean("<rect/>");

        clean.ShouldContain(SvgNs);
        clean.ShouldContain("<rect");
    }
}
