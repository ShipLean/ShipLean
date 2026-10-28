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
        foreach (var tag in item.Tags)
        {
            xml.Append("  <label>").Append(AgentXml.Escape(tag)).Append("</label>\n");
        }
        if (input.Parent is { } parent)
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

        xml.Append("<guidance/>\n");
        xml.Append("<repository url=\"").Append(AgentXml.Escape(input.Repository.Url))
            .Append("\" base-ref=\"").Append(AgentXml.Escape(input.Repository.BaseRef)).Append("\"/>");
        return xml.ToString();
    }
}
