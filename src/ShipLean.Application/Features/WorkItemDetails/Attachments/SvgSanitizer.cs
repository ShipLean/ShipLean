using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace ShipLean.Application.Features.WorkItemDetails.Attachments;

// Makes an uploaded SVG safe to show: scripts and other active elements, event attributes, and every reference that
// leaves the document (anything but a "#fragment" link, and any CSS url()/@import to elsewhere) are removed.
internal static partial class SvgSanitizer
{
    private static readonly HashSet<string> BlockedElements = ["script", "foreignObject", "iframe", "object", "embed", "set", "animate", "animateMotion", "animateTransform", "handler", "listener"];

    private const string SvgNamespace = "http://www.w3.org/2000/svg";

    // Deeper than any real drawing; a deeper document is refused rather than walked.
    private const int MaxDepth = 256;

    // False when the bytes are not an SVG document (not XML, a DTD, nested deeper than MaxDepth, or a root outside the SVG namespace); the caller then keeps them as an opaque download.
    public static bool TrySanitize(Stream svg, out byte[] clean)
    {
        clean = [];
        XDocument document;
        try
        {
            if (!IsShallow(svg))
            {
                return false;
            }

            svg.Position = 0;
            using var reader = XmlReader.Create(svg, Settings());
            document = XDocument.Load(reader);
        }
        catch (XmlException)
        {
            return false;
        }

        if (document.Root is not { Name: { LocalName: "svg", NamespaceName: SvgNamespace } } root)
        {
            return false;
        }

        Clean(root);
        using var output = new MemoryStream();
        using (var writer = XmlWriter.Create(output, new XmlWriterSettings { Encoding = new UTF8Encoding(false), OmitXmlDeclaration = true }))
        {
            document.Root.WriteTo(writer);
        }

        clean = output.ToArray();
        return true;
    }

    private static XmlReaderSettings Settings() => new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };

    // A pass over the nodes that only watches the depth, so the tree is never built (or walked) for a hostile nesting.
    private static bool IsShallow(Stream svg)
    {
        using var reader = XmlReader.Create(svg, Settings());
        while (reader.Read())
        {
            if (reader.Depth > MaxDepth)
            {
                return false;
            }
        }

        return true;
    }

    // Iterative: the depth limit bounds the tree, but the walk does not rely on the stack either.
    private static void Clean(XElement root)
    {
        foreach (var element in root.DescendantsAndSelf().ToList())
        {
            if (element != root && (element.Name.NamespaceName != SvgNamespace || BlockedElements.Contains(element.Name.LocalName) || IsExternalStyle(element)))
            {
                element.Remove();
                continue;
            }

            element.Attributes().Where(IsUnsafe).Remove();
        }
    }

    private static bool IsExternalStyle(XElement element)
        => element.Name.LocalName == "style" && ExternalCss().IsMatch(element.Value);

    private static bool IsUnsafe(XAttribute attribute)
    {
        var name = attribute.Name.LocalName;
        if (name.StartsWith("on", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (name == "href" || name == "src")
        {
            return !attribute.Value.TrimStart().StartsWith('#');
        }

        return ExternalCss().IsMatch(attribute.Value) || attribute.Value.Contains("javascript:", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"@import|url\((?>\s*['""]?\s*)(?!#)", RegexOptions.IgnoreCase)]
    private static partial Regex ExternalCss();
}
