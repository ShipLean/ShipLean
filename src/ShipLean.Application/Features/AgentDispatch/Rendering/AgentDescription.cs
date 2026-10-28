using System.Text;

namespace ShipLean.Application.Features.AgentDispatch.Rendering;

internal static class AgentDescription
{
    // The description with the acceptance criteria appended as a Markdown checklist. Null when there is neither.
    public static string? WithCriteria(string? description, IReadOnlyList<AgentCriterionInput> criteria)
    {
        var text = string.IsNullOrWhiteSpace(description) ? null : description.TrimEnd();
        if (criteria.Count == 0)
        {
            return text;
        }
        var builder = new StringBuilder(text ?? string.Empty);
        if (text != null)
        {
            builder.Append("\n\n");
        }
        builder.Append("## Acceptance criteria");
        foreach (var criterion in criteria)
        {
            builder.Append(criterion.Done ? "\n- [x] " : "\n- [ ] ").Append(criterion.Text);
        }
        return builder.ToString();
    }
}
