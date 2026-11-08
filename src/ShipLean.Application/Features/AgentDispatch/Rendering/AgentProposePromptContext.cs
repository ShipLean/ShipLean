using System.Globalization;
using System.Text;
using ShipLean.Contracts.Agents;

namespace ShipLean.Application.Features.AgentDispatch.Rendering;

// A propose session's promptContext (D10, D17), rendered only from the allowlisted input: the GitHub issue (repository, number,
// author login, title, body), the comments mirrored from it by GitHub login, the related items (key, type, state, title, pull
// request URLs, merge commits), the guidance and the code repository the relay checks out. Nothing else of the item is rendered.
internal static class AgentProposePromptContext
{
    public static string Render(AgentContextInput input, AgentProposeInput propose)
    {
        var xml = new StringBuilder();
        xml.Append("<github-issue repository=\"").Append(AgentXml.Escape(propose.Repository))
            .Append("\" number=\"").Append(propose.Number.ToString(CultureInfo.InvariantCulture))
            .Append("\" author-login=\"").Append(AgentXml.Escape(propose.AuthorLogin)).Append("\">\n");
        xml.Append("  <title>").Append(AgentXml.Escape(input.Item.Title)).Append("</title>\n");
        xml.Append("  <body>").Append(AgentXml.Escape(input.Item.Description)).Append("</body>\n");
        xml.Append("</github-issue>\n");

        if (input.Comments.Count > 0)
        {
            xml.Append("<github-comments>\n");
            foreach (var comment in input.Comments.OrderBy(c => c.CreatedAt))
            {
                var created = comment.CreatedAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                xml.Append("  <comment author-login=\"").Append(AgentXml.Escape(comment.Author.Name))
                    .Append("\" created-at=\"").Append(created).Append("\">")
                    .Append(AgentXml.Escape(comment.Body)).Append("</comment>\n");
            }
            xml.Append("</github-comments>\n");
        }

        AppendRelated(xml, propose.Related);

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

    private static void AppendRelated(StringBuilder xml, IReadOnlyList<AgentRelatedWork> related)
    {
        if (related.Count == 0)
        {
            return;
        }
        xml.Append("<related-items>\n");
        foreach (var item in related)
        {
            xml.Append("  <item identifier=\"").Append(AgentXml.Escape(item.Key)).Append("\" type=\"").Append(AgentXml.Escape(item.Type))
                .Append("\" state=\"").Append(AgentXml.Escape(item.State)).Append("\">\n");
            xml.Append("    <title>").Append(AgentXml.Escape(item.Title)).Append("</title>\n");
            foreach (var url in item.PullRequestUrls)
            {
                xml.Append("    <pull-request url=\"").Append(AgentXml.Escape(url)).Append("\"/>\n");
            }
            foreach (var commit in item.MergeCommits)
            {
                xml.Append("    <merge-commit sha=\"").Append(AgentXml.Escape(commit.Sha)).Append('"');
                if (commit.MergedAt is { } merged)
                {
                    xml.Append(" merged-at=\"").Append(merged.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)).Append('"');
                }
                xml.Append("/>\n");
            }
            xml.Append("  </item>\n");
        }
        xml.Append("</related-items>\n");
    }
}
