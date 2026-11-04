using System.Text;
using ShipLean.Application.Features.WorkItemDetails.Attachments;

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
    [TestCase("<foreignObject><div xmlns=\"http://www.w3.org/1999/xhtml\">evil.example</div></foreignObject>")]
    [TestCase("<script>evil.example</script>")]
    [TestCase("<rect onclick=\"evil.example\"/>")]
    public void TrySanitize_WhenHostileConstruct_ShouldRemoveIt(string inner)
    {
        var clean = Clean(inner);

        clean.ShouldNotContain("evil.example");
        clean.ShouldNotContain("javascript", Case.Insensitive);
        clean.ShouldNotContain("foreignObject");
    }

    [Test]
    public void TrySanitize_WhenLocalUrlReference_ShouldKeepIt()
    {
        var clean = Clean("<defs><linearGradient id=\"g\"/></defs><rect fill=\"url(#g)\" style=\"stroke:url(#g)\"/>");

        clean.ShouldContain("fill=\"url(#g)\"");
        clean.ShouldContain("stroke:url(#g)");
    }
}
