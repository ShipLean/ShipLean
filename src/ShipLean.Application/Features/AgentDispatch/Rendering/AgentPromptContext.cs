using System.Globalization;
using System.Text;

namespace ShipLean.Application.Features.AgentDispatch.Rendering;

// Linear's XML-like promptContext. ShipLean comments are not threaded, so all of them form one
// <primary-directive-thread>, in time order, keyed by the triggering comment (or the latest one).
internal static class AgentPromptContext
{
    public static string Render(AgentContextInput input)
    {
        var xml = new StringBuilder();
        var item = input.Item;
        xml.Append("<issue identifier=\"").Append(AgentXml.Escape(item.Key)).Append("\">\n");
        xml.Append("  <title>").Append(AgentXml.Escape(item.Title)).Append("</title>\n");
        xml.Append("  <description>").Append(AgentXml.Escape(AgentDescription.WithCriteria(item.Description, item.Criteria))).Append("</description>\n");
        xml.Append("  <team name=\"").Append(AgentXml.Escape(input.Project.Name)).Append("\"/>\n");
        AppendDetails(xml, input);
        foreach (var tag in item.Tags)
        {
            xml.Append("  <label>").Append(AgentXml.Escape(tag)).Append("</label>\n");
        }
        AppendLinks(xml, item.Links ?? []);
        AppendChildren(xml, item.Children ?? []);
        AppendAttachments(xml, item.Attachments ?? []);
        if (input.Ancestors is { } ancestors)
        {
            AppendAncestors(xml, ancestors);
        }
        else if (input.Parent is { } parent)
        {
            xml.Append("  <parent-issue identifier=\"").Append(AgentXml.Escape(parent.Key)).Append("\">\n");
            xml.Append("    <title>").Append(AgentXml.Escape(parent.Title)).Append("</title>\n");
            xml.Append("    <description>").Append(AgentXml.Escape(parent.Description)).Append("</description>\n");
            xml.Append("  </parent-issue>\n");
        }
        if (input.Feature is { } feature)
        {
            xml.Append("  <project name=\"").Append(AgentXml.Escape(feature.Title)).Append("\"/>\n");
        }
        xml.Append("</issue>\n");

        if (input.Comments.Count > 0)
        {
            var ordered = input.Comments.OrderBy(c => c.CreatedAt).ToList();
            var threadId = input.TriggerCommentId ?? ordered[^1].Id;
            xml.Append("<primary-directive-thread comment-id=\"").Append(AgentXml.Escape(threadId)).Append("\">\n");
            foreach (var comment in ordered)
            {
                var created = comment.CreatedAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                xml.Append("  <comment author=\"").Append(AgentXml.Escape(comment.Author.Name))
                    .Append("\" created-at=\"").Append(created).Append("\">")
                    .Append(AgentXml.Escape(comment.Body)).Append("</comment>\n");
            }
            xml.Append("</primary-directive-thread>\n");
        }

        if (string.IsNullOrWhiteSpace(input.Guidance))
        {
            xml.Append("<guidance/>\n");
        }
        else
        {
            xml.Append("<guidance>").Append(AgentXml.Escape(input.Guidance)).Append("</guidance>\n");
        }
        xml.Append("<repository url=\"").Append(AgentXml.Escape(input.Repository.Url))
            .Append("\" base-ref=\"").Append(AgentXml.Escape(input.Repository.BaseRef)).Append("\"/>");
        return xml.ToString();
    }

    // Type, state (the move that fired the session when there is one, else the current state), assignee, sprint and points; each only when known.
    private static void AppendDetails(StringBuilder xml, AgentContextInput input)
    {
        var item = input.Item;
        if (!string.IsNullOrEmpty(item.Type))
        {
            xml.Append("  <type>").Append(AgentXml.Escape(item.Type)).Append("</type>\n");
        }
        if (input.Transition is { } move)
        {
            xml.Append("  <state from=\"").Append(AgentXml.Escape(move.From)).Append("\" to=\"").Append(AgentXml.Escape(move.To)).Append("\"/>\n");
        }
        else if (!string.IsNullOrEmpty(item.State))
        {
            xml.Append("  <state name=\"").Append(AgentXml.Escape(item.State)).Append("\"/>\n");
        }
        if (!string.IsNullOrEmpty(item.Assignee))
        {
            xml.Append("  <assignee>").Append(AgentXml.Escape(item.Assignee)).Append("</assignee>\n");
        }
        if (!string.IsNullOrEmpty(item.Sprint))
        {
            xml.Append("  <sprint>").Append(AgentXml.Escape(item.Sprint)).Append("</sprint>\n");
        }
        if (item.Points is { } points)
        {
            xml.Append("  <points>").Append(points.ToString("0.##", CultureInfo.InvariantCulture)).Append("</points>\n");
        }
    }

    private static void AppendLinks(StringBuilder xml, IReadOnlyList<AgentLinkInput> links)
    {
        foreach (var link in links)
        {
            xml.Append("  <link type=\"").Append(AgentXml.Escape(link.Type)).Append('"');
            if (link.Identifier != null)
            {
                xml.Append(" identifier=\"").Append(AgentXml.Escape(link.Identifier)).Append('"');
            }
            xml.Append(" url=\"").Append(AgentXml.Escape(link.Url)).Append("\">").Append(AgentXml.Escape(link.Title)).Append("</link>\n");
        }
    }

    private static void AppendChildren(StringBuilder xml, IReadOnlyList<AgentChildInput> children)
    {
        if (children.Count == 0)
        {
            return;
        }
        xml.Append("  <children>\n");
        foreach (var child in children)
        {
            xml.Append("    <child identifier=\"").Append(AgentXml.Escape(child.Key)).Append("\" state=\"").Append(AgentXml.Escape(child.State))
                .Append("\" url=\"").Append(AgentXml.Escape(child.Url)).Append("\">").Append(AgentXml.Escape(child.Title)).Append("</child>\n");
        }
        xml.Append("  </children>\n");
    }

    // A Markdown attachment that fit the cap carries its text; any other is just name, type, size and url.
    private static void AppendAttachments(StringBuilder xml, IReadOnlyList<AgentAttachmentInput> attachments)
    {
        if (attachments.Count == 0)
        {
            return;
        }
        xml.Append("  <attachments>\n");
        foreach (var attachment in attachments)
        {
            xml.Append("    <attachment name=\"").Append(AgentXml.Escape(attachment.Name)).Append("\" content-type=\"").Append(AgentXml.Escape(attachment.ContentType))
                .Append("\" size-bytes=\"").Append(attachment.SizeBytes.ToString(CultureInfo.InvariantCulture)).Append("\" url=\"").Append(AgentXml.Escape(attachment.Url));
            if (attachment.Text is null)
            {
                xml.Append("\"/>\n");
            }
            else
            {
                xml.Append("\">").Append(AgentXml.Escape(attachment.Text)).Append("</attachment>\n");
            }
        }
        xml.Append("  </attachments>\n");
    }

    // The whole chain above the item, nearest first.
    private static void AppendAncestors(StringBuilder xml, IReadOnlyList<AgentRelatedItemInput> ancestors)
    {
        if (ancestors.Count == 0)
        {
            return;
        }
        xml.Append("  <ancestors>\n");
        foreach (var ancestor in ancestors)
        {
            xml.Append("    <ancestor identifier=\"").Append(AgentXml.Escape(ancestor.Key)).Append('"');
            if (!string.IsNullOrEmpty(ancestor.Type))
            {
                xml.Append(" type=\"").Append(AgentXml.Escape(ancestor.Type)).Append('"');
            }
            xml.Append(" url=\"").Append(AgentXml.Escape(ancestor.Url)).Append("\">\n");
            xml.Append("      <title>").Append(AgentXml.Escape(ancestor.Title)).Append("</title>\n");
            xml.Append("      <description>").Append(AgentXml.Escape(ancestor.Description)).Append("</description>\n");
            xml.Append("    </ancestor>\n");
        }
        xml.Append("  </ancestors>\n");
    }
}
